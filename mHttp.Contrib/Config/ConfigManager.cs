using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using m.Deploy;

namespace m.Config
{
    public static class ConfigManager
    {
        static ConfigMap GetConfigMap<TConf>() where TConf : IConfigurable, new()
        {
            return GetConfigMap(typeof(TConf));
        }

        internal static ConfigMap GetConfigMap(Type confType)
        {
            var deployConfigLabel = (DeployConfigLabelAttribute)confType.GetCustomAttributes(typeof(DeployConfigLabelAttribute), false)
                                                                        .SingleOrDefault();
            var properties = confType.GetProperties();

            var configMapEntries = properties.Select(p => new {
                                                  Property = p,
                                                  EnvironmentVariable = p.GetCustomAttributes(typeof(EnvironmentVariableAttribute), false)
                                                                         .SingleOrDefault() as EnvironmentVariableAttribute
                                              })
                                             .Where(p => p.EnvironmentVariable != null)
                                             .Select(p => new ConfigMap.Entry(p.Property,
                                                                              p.EnvironmentVariable,
                                                                              TypeDescriptor.GetConverter(p.Property.PropertyType)))
                                             .ToList();
            
            return new ConfigMap(confType, deployConfigLabel, configMapEntries);
        }
        
        public static void Apply<TConf>(TConf conf) where TConf : IConfigurable, new()
        {
            var configMapEntries = GetConfigMap<TConf>();
            var entriesToApply = configMapEntries.Select(e => new {
                                        e.Property,
                                        e.Converter,
                                        EnvironmentValueString = e.EnvironmentVariable.GetEnvironmentValue()
                                    })
                                    .Where(e => !string.IsNullOrEmpty(e.EnvironmentValueString));

            foreach (var entry in entriesToApply)
            {
                try
                {
                    object propertyValue = entry.Converter.ConvertFromString(entry.EnvironmentValueString);
                    entry.Property.SetValue(conf, propertyValue);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"Error setting property:[{entry.Property.Name}] with environment value:[{entry.EnvironmentValueString}] for conf object of type:[{typeof(TConf)}]", e);
                }
            }
        }

        public static TConf Load<TConf>() where TConf : IConfigurable, new()
        {
            var config = new TConf();
            Apply(config);

            return config;
        }
    }
}
