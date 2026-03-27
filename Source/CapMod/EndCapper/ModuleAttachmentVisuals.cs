using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AttachmentVisuals
{
    public class ModuleAttachmentVisuals : PartModule
    {
        // todo: dynamically add buttons for manual control of each registered
        // attachment node (only when advanced tweakables is enabled). Will
        // need implement IConfigNode on NodeData so state can be saved rather than inferred.

        // todo: serialise per-node state so that they never change during flight.

        public class NodeVisual
        {
            public AttachNode attachNode;
            public List<Transform> showWhenAttached;
            public List<Transform> showWhenFree;

            public bool Load(Part part, ConfigNode configNode)
            {
                string name = "";
                if (!configNode.TryGetValue("name", ref name))
                {
                    Debug.LogError("[ModuleAttachmentVisuals]: NodeVisuals is missing an attachment node name.");
                    return false;
                }

                attachNode = part.FindAttachNode(name);
                if (attachNode == null)
                {
                    Debug.LogError($"[ModuleAttachmentVisuals]: Node '{name}' not found on part '{part.name}'");
                    return false;
                }

                return TryLoadList(ref showWhenAttached, part, configNode, "showWhenAttached")
                    | TryLoadList(ref showWhenFree, part, configNode, "showWhenFree");
            }

            private bool TryLoadList(ref List<Transform> list, Part part, ConfigNode configNode, string listName)
            {
                string listString = null;
                if (!configNode.TryGetValue(listName, ref listString) || string.IsNullOrWhiteSpace(listString))
                    return false;

                foreach (string transformName in listString.Split(','))
                {
                    Transform t = part.FindModelTransform(transformName.Trim());

                    if (t != null)
                        (list ??= new List<Transform>()).Add(t);
                    else
                        Debug.LogError($"[ModuleAttachmentVisuals]: Could not find transform '{transformName}' on {part.name}");
                }

                return true;
            }

            public void UpdateVisibility()
            {
                if (attachNode != null)
                    ApplyVisibility(attachNode.attachedPart != null);
            }

            public void ApplyVisibility(bool attached)
            {
                showWhenAttached?.ForEach(t => t?.gameObject.SetActive(attached));
                showWhenFree?.ForEach(t => t?.gameObject.SetActive(!attached));
            }
        }

        [NonSerialized] public List<NodeVisual> nodeVisuals;
        [SerializeField] private string[] nodeVisualConfigs;
        private HashSet<Part> directChildren;

        // --- Lifecycle ---

        public override void OnLoad(ConfigNode node)
        {
            if (nodeVisualConfigs != null)
                return;

            ConfigNode[] nodesCfg = node.GetNodes("NODEVISUAL");
            if (nodesCfg.Length > 0)
                nodeVisualConfigs = nodesCfg.Select(n => n.ToString()).ToArray();
        }

        public override void OnStart(StartState state)
        {
            if (nodeVisualConfigs == null)
                return;

            for (int i = 0; i < nodeVisualConfigs.Length; i++)
            {
                try
                {
                    var newNode = new NodeVisual();

                    if (newNode.Load(part, ConfigNode.Parse(nodeVisualConfigs[i]).GetNode("NODEVISUAL")))
                        (nodeVisuals ??= new List<NodeVisual>()).Add(newNode);
                }
                catch { }
            }

            // Module doesn't need to do anything from here if no valid node visuals were found.
            if (nodeVisuals == null)
                return;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartEvent.Add(OnEditorEvent);
                directChildren = part.children.ToHashSet();
            }

            UpdateVisuals();
        }

        public void OnDestroy()
        {
            if (nodeVisuals != null && HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorPartEvent.Remove(OnEditorEvent);
        }

        // --- Functions ---

        private void UpdateVisuals() => nodeVisuals?.ForEach(n => n.UpdateVisibility());

        private void OnEditorEvent(ConstructionEventType evt, Part p)
        {
            if (evt != ConstructionEventType.PartAttached && evt != ConstructionEventType.PartDetached)
                return;

            if (p == part
                || (evt == ConstructionEventType.PartAttached && p.parent == part && directChildren.Add(p))
                || (evt == ConstructionEventType.PartDetached && directChildren.Remove(p)))
            {
                UpdateVisuals();
            }
        }
    }
}
