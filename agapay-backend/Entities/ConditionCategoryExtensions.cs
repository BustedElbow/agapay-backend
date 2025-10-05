using System.Collections.Generic;

namespace agapay_backend.Entities
{
    public static class ConditionCategoryExtensions
    {
        private static readonly IReadOnlyList<(ConditionCategory Category, string Label)> _orderedDisplay =
            new List<(ConditionCategory, string)>
            {
                (ConditionCategory.Neurological, "Neurological Conditions"),
                (ConditionCategory.Musculoskeletal, "Musculoskeletal Conditions"),
                (ConditionCategory.Pediatric, "Pediatric Conditions"),
                (ConditionCategory.Geriatric, "Geriatric Conditions"),
                (ConditionCategory.Other, "Others")
            };

        private static readonly IReadOnlyDictionary<ConditionCategory, string> _labelLookup = CreateLookup(_orderedDisplay);

        public static string ToDisplayLabel(this ConditionCategory category)
        {
            return _labelLookup.TryGetValue(category, out var label)
                ? label
                : category.ToString();
        }

        public static IReadOnlyList<(ConditionCategory Category, string Label)> OrderedCategories => _orderedDisplay;

        private static IReadOnlyDictionary<ConditionCategory, string> CreateLookup(IEnumerable<(ConditionCategory Category, string Label)> source)
        {
            var dictionary = new Dictionary<ConditionCategory, string>();
            foreach (var (category, label) in source)
            {
                dictionary[category] = label;
            }

            return dictionary;
        }
    }
}
