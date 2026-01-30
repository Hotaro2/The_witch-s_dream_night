using System.Collections.Generic;

namespace VN
{
    public sealed class VNLine
    {
        public string Speaker { get; }
        public string Text { get; }
        public IReadOnlyList<string> Tags { get; }

        public VNLine(string speaker, string text, IReadOnlyList<string> tags)
        {
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
            Tags = tags;
        }
    }
}