using System;
using UnityEngine;

namespace Core.UI
{
    [CreateAssetMenu(fileName = "MenuGroup", menuName = "UI/Menu Group")]
    public class MenuGroupDefinition : ScriptableObject
    {
        [SerializeField]
        private string groupId;

        [SerializeField]
        private string displayName;

        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? name : groupId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
