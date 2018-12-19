namespace m.Http.Routing
{
    interface ITemplatePart
    {
        int CompareWeight { get; }
    }

    class Literal : ITemplatePart
    {
        public readonly string Value;

        public Literal(string value) { Value = value; }

        public int CompareWeight => 1;

        public override string ToString() => $"Literal({Value})";
    }

    class Variable : ITemplatePart
    {
        public readonly string Name;

        public Variable(string name) { Name = name; }

        public int CompareWeight => 2;

        public override string ToString() => $"Variable({Name})";
    }

    class Wildcard : ITemplatePart
    {
        public static readonly Wildcard Instance = new Wildcard();

        Wildcard() { }

        public int CompareWeight => 3;

        public override string ToString() => "Wildcard";
    }
}
