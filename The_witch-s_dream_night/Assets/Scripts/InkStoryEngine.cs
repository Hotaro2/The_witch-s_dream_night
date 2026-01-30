using System;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

namespace VN
{
    public sealed class InkStoryEngine
    {
        private Story story;

        public bool IsInitialized => story != null;

        public void Initialize(TextAsset compiledInkJson)
        {
            if (compiledInkJson == null)
                throw new ArgumentNullException(nameof(compiledInkJson));

            story = new Story(compiledInkJson.text);
        }

        public bool CanContinue()
        {
            EnsureInitialized();
            return story.canContinue;
        }

        public VNLine ContinueLine()
        {
            EnsureInitialized();

            string raw = story.Continue() ?? string.Empty;
            raw = raw.TrimEnd('\n', '\r');

            var tags = new List<string>(story.currentTags);
            string speaker = ExtractSpeakerFromTags(tags);

            return new VNLine(speaker, raw, tags);
        }

        public IReadOnlyList<Choice> GetCurrentChoices()
        {
            EnsureInitialized();
            return story.currentChoices;
        }

        public void ChooseChoiceIndex(int index)
        {
            EnsureInitialized();
            story.ChooseChoiceIndex(index);
        }

        public void JumpTo(string knotName)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(knotName)) return;

            story.ChoosePathString(knotName.Trim());
        }

        private static string ExtractSpeakerFromTags(List<string> tags)
        {
            // 태그 예시:
            // #speaker Amy
            // #speaker: Amy
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (string.IsNullOrWhiteSpace(t)) continue;

                t = t.Trim();
                if (t.StartsWith("#")) t = t.Substring(1).Trim();

                if (t.StartsWith("speaker", StringComparison.OrdinalIgnoreCase))
                {
                    // speaker Amy
                    // speaker: Amy
                    int colon = t.IndexOf(':');
                    if (colon >= 0)
                        return t.Substring(colon + 1).Trim();

                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        return parts[1].Trim();
                }
            }

            return string.Empty;
        }

        private void EnsureInitialized()
        {
            if (story == null)
                throw new InvalidOperationException("InkStoryEngine is not initialized. Assign a compiled ink JSON TextAsset first.");
        }
    }
}