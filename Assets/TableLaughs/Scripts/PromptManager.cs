using System;
using System.Collections.Generic;
using UnityEngine;

namespace TableLaughs
{
    public sealed class PromptManager : MonoBehaviour
    {
        private const string DefaultPromptResourcePath = "Prompts/table_laughs_prompts";

        [SerializeField] private TextAsset promptPackAsset;

        private PromptPackData promptPack;
        private readonly List<PromptEntry> standardBag = new List<PromptEntry>();
        private readonly List<PromptEntry> finalBag = new List<PromptEntry>();
        private System.Random random;

        public string PackName => promptPack != null ? promptPack.packName : "Table Laughs Starter Pack";

        public void LoadPromptPack()
        {
            if (promptPackAsset == null)
            {
                promptPackAsset = Resources.Load<TextAsset>(DefaultPromptResourcePath);
            }

            if (promptPackAsset == null)
            {
                throw new InvalidOperationException($"Prompt pack missing at Resources/{DefaultPromptResourcePath}.json");
            }

            promptPack = JsonUtility.FromJson<PromptPackData>(promptPackAsset.text);
            if (promptPack == null || promptPack.prompts == null || promptPack.prompts.Length == 0)
            {
                throw new InvalidOperationException("Prompt pack loaded, but no standard prompts were found.");
            }

            if (promptPack.finalPrompts == null || promptPack.finalPrompts.Length == 0)
            {
                throw new InvalidOperationException("Prompt pack loaded, but no final prompts were found.");
            }

            random = new System.Random(Environment.TickCount);
            ResetForGame();
        }

        public void ResetForGame()
        {
            if (promptPack == null)
            {
                LoadPromptPack();
                return;
            }

            standardBag.Clear();
            standardBag.AddRange(promptPack.prompts);
            Shuffle(standardBag);

            finalBag.Clear();
            finalBag.AddRange(promptPack.finalPrompts);
            Shuffle(finalBag);
        }

        public PromptEntry DrawStandardPrompt()
        {
            if (standardBag.Count == 0)
            {
                standardBag.AddRange(promptPack.prompts);
                Shuffle(standardBag);
            }

            var prompt = standardBag[0];
            standardBag.RemoveAt(0);
            return prompt;
        }

        public PromptEntry DrawFinalPrompt()
        {
            if (finalBag.Count == 0)
            {
                finalBag.AddRange(promptPack.finalPrompts);
                Shuffle(finalBag);
            }

            var prompt = finalBag[0];
            finalBag.RemoveAt(0);
            return prompt;
        }

        public string GetRandomFallbackAnswer()
        {
            if (promptPack == null || promptPack.randomAnswers == null || promptPack.randomAnswers.Length == 0)
            {
                return "A very confident sandwich";
            }

            return promptPack.randomAnswers[random.Next(promptPack.randomAnswers.Length)];
        }

        private void Shuffle<T>(IList<T> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }
    }
}
