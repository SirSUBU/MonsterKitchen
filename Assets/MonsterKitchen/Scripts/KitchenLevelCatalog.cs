// The ordered list shown on the cookbook's level-select pages.
// Add another level asset here when its scene is ready; the menu creates its own extra pages.
using System.Collections.Generic;
using UnityEngine;

namespace MonsterKitchen
{
    [CreateAssetMenu(menuName = "Monster Kitchen/Level catalog", fileName = "KitchenLevels")]
    public sealed class KitchenLevelCatalog : ScriptableObject
    {
        public KitchenLevel[] Levels = new KitchenLevel[0];

        // Ignore empty slots, drafts and an asset added twice. Work on a new list so opening
        // the menu never edits the catalog saved on disk or changes the designer's ordering.
        public List<KitchenLevel> PublishedLevels()
        {
            var result = new List<KitchenLevel>();
            if (Levels == null)
                return result;
            foreach (var level in Levels)
                if (level && level.IsPublished && !result.Contains(level))
                    result.Add(level);
            return result;
        }
    }
}
