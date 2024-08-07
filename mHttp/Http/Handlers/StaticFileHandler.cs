﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;

using m.Http.Extensions;
using System.Text.RegularExpressions;
using m.Logging;
using MimeKit;

namespace m.Http.Handlers
{
    public class StaticFileHandler // not threadsafe really, but good for now
    {
        class CachedFile
        {
            public FileResponse.Buffered Response { get; }
            public HttpResponse GZippedResponse { get; }

            public DateTime LastModified { get; }
            public string ContentType { get; }

            public CachedFile(FileResponse.Buffered response, HttpResponse gzippedResponse)
            {
                Response = response;
                LastModified = response.LastModified;
                ContentType = response.ContentType;
                GZippedResponse = gzippedResponse;
            }
        }

        static readonly HttpResponse NotFound = new HttpResponse(HttpStatusCode.NotFound);
        static readonly HttpResponse Forbidden = new HttpResponse(HttpStatusCode.Forbidden);

        readonly int reqPathFilenameStartIndex;
        readonly string directory;
        readonly Func<HttpResponse, HttpResponse> gzipFunc;
        readonly int maxFileLengthToCache=32768;

        readonly ConcurrentDictionary<string, CachedFile> cache;

        public StaticFileHandler(int reqPathFilenameStartIndex,
                                 DirectoryInfo dirInfo,
                                 Func<HttpResponse, HttpResponse> gzipFunc,
                                 int maxFileLengthToCache=32768)
        {
            this.reqPathFilenameStartIndex = reqPathFilenameStartIndex; // Position (string index) of incoming `request.Path` at which the requested filename is expected to begin
            directory = dirInfo.FullName;                               // Directory root to serve
            this.gzipFunc = gzipFunc;                                   // Supply identity function if you want to skip gzipping
            this.maxFileLengthToCache = maxFileLengthToCache;

            cache = new ConcurrentDictionary<string, CachedFile>(StringComparer.Ordinal);
        }

        //Matches the protocol part of URLS (http(s)://) or absolute Win32 paths (X:\), or attempts at relative paths (..). 
        //Linux paths when Path.Combined with the root directory never end up with / so that's fine. (/etc/passwd becomes /mySafeWebroot/etc/passwd) and promptly 404s.
        private static readonly Regex BadFileRegex = new Regex(@"^[a-z]+:\/\/|[a-z]:\\"); 

        private static bool VerifyPathAcceptable(string filename) => !BadFileRegex.IsMatch(filename);
      

        public HttpResponse Handle(IHttpRequest req)
        {
            var requestedFilename = WebUtility.UrlDecode(req.Path.Substring(reqPathFilenameStartIndex));

            if (!VerifyPathAcceptable(requestedFilename))
            {

                return NotFound;
            }

            var absFilename = Path.GetFullPath(Path.Combine(directory, requestedFilename));
            if (!absFilename.StartsWith(directory))
            {
                return Forbidden;
            }

            var fileInfo = new FileInfo(absFilename);
            if (!fileInfo.Exists) // cached but deleted
            {
                return NotFound;
            }

            HttpResponse response;

            if (req.TryGetIfLastModifiedSince(out DateTime ifModifiedSince) && fileInfo.LastWriteTimeUtc <= ifModifiedSince)
            {
                // Remote client (browser) has a valid cached copy (we may not have it in cache yet though eg. server restarted)
                response = new HttpResponse(HttpStatusCode.NotModified, MimeTypes.GetMimeType(fileInfo.Name));
            }
            else if (cache.TryGetValue(absFilename, out CachedFile cachedFile) && fileInfo.LastWriteTimeUtc <= cachedFile.LastModified)
            {
                // Dictionary cached copy is still valid
                response = req.IsAcceptGZip() ? cachedFile.GZippedResponse : cachedFile.Response;
            }
            else
            {
                // Load fresh from disk
                if (fileInfo.Length <= maxFileLengthToCache)
                {
                    var fileResponse = new FileResponse.Buffered(fileInfo);
                    var gzippedFileResponse = gzipFunc(fileResponse);
                    cache[absFilename] = new CachedFile(fileResponse, gzippedFileResponse);
                    response = req.IsAcceptGZip() ? gzippedFileResponse : fileResponse;
                }
                else
                {
                    response = new FileResponse.Stream(fileInfo);
                }
            }

            return response;
        }
    }
}
