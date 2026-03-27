using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttachmentVisuals
{
    public class ModuleAttachmentVisuals : PartModule
    {
        // todo: dynamically add buttons for manual control of each registered
        // attachment node (only when advanced tweakables is enabled). Will
        // need implement IConfigNode on NodeData so state can be saved rather than inferred.

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

                bool valid = false;
                valid = valid || TryLoadlist(ref showWhenAttached, part, configNode, "showWhenAttached");
                valid = valid || TryLoadlist(ref showWhenFree, part, configNode, "showWhenFree");

                return valid;
            }

            private bool TryLoadlist(ref List<Transform> list, Part part, ConfigNode configNode, string listName)
            {
                string listString = null;
                if (!configNode.TryGetValue(listName, ref listString))
                    return false;

                if (string.IsNullOrWhiteSpace(listString))
                    return false;

                string[] transformList = listString.Split(',');

                if (transformList.Length < 1)
                    return false;

                foreach (string transformName in transformList)
                {
                    Transform t = part.FindModelTransform(transformName.Trim());

                    if (t != null)
                        (list ?? (list = new List<Transform>())).Add(t);
                    else
                        Debug.LogError($"[ModuleAttachmentVisuals]: Could not find transform '{transformName}' on {part.name}");
                }

                return true;
            }

            public void ApplyVisiblity(bool attached)
            {
                ApplyList(showWhenAttached, attached);
                ApplyList(showWhenFree, !attached);
            }

            private void ApplyList(List<Transform> list, bool show)
            {
                if (list == null)
                    return;

                foreach (var t in list)
                {
                    if (t == null)
                        continue;

                    t.gameObject.SetActive(show);
                }
            }
        }

        [SerializeField]
        private string[] nodeVisualConfigs;

        [NonSerialized]
        public List<NodeVisual> nodeVisuals;

        private HashSet<Part> directChildren;

        // --- Lifecycle ---

        public override void OnLoad(ConfigNode node)
        {
            ConfigNode[] nodesCfg = node.GetNodes("NODEVISUAL");
            if (nodesCfg.Length > 0)
            {
                nodeVisualConfigs = new string[nodesCfg.Length];
                for (int i = 0; i < nodesCfg.Length; i++)
                    nodeVisualConfigs[i] = nodesCfg[i].ToString();
            }
        }

        public override void OnStart(StartState state)
        {
            if (nodeVisualConfigs == null)
                return;

            for (int i = 0; i < nodeVisualConfigs.Length; i++)
            {
                try
                {
                    NodeVisual newNode = new NodeVisual();

                    if (newNode.Load(part, ConfigNode.Parse(nodeVisualConfigs[i]).GetNode("NODEVISUAL")))
                        (nodeVisuals ?? (nodeVisuals = new List<NodeVisual>())).Add(newNode);
                }
                catch { }
            }

            // Module doesn't need to do anything from here if no valid node visuals were found.
            if (nodeVisuals == null)
                return;

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartEvent.Add(OnEditorEvent);
                CacheInitialChildren();
            }

            UpdateVisuals();
        }

        public void OnDestroy()
        {
            if (nodeVisuals == null)
                return;

            if (HighLogic.LoadedSceneIsEditor)
                GameEvents.onEditorPartEvent.Remove(OnEditorEvent);
        }

        // --- Functions ---

        private void UpdateVisuals()
        {
            if (nodeVisuals == null)
                return;

            foreach (var nodeData in nodeVisuals)
            {
                if (nodeData.attachNode == null)
                    continue;

                nodeData.ApplyVisiblity(nodeData.attachNode.attachedPart != null);
            }
        }

        private void OnEditorEvent(ConstructionEventType evt, Part p)
        {
            // Only care about attach/detach
            if (evt != ConstructionEventType.PartAttached && evt != ConstructionEventType.PartDetached)
                return;

            // If the event is on this part
            if (part == p)
            {
                UpdateVisuals();
                return;
            }

            // Only care about events involving this parts direct relatives
            switch (evt)
            {
                case ConstructionEventType.PartAttached:
                    if (p.parent == part)
                    {
                        directChildren.Add(p);
                        UpdateVisuals();
                    }
                    break;

                case ConstructionEventType.PartDetached:
                    if (directChildren.Contains(p))
                    {
                        directChildren.Remove(p);
                        UpdateVisuals();
                    }
                    break;
            }
        }

        private void CacheInitialChildren()
        {
            // Populate children at start to track later
            directChildren = new HashSet<Part>();

            foreach (var child in part.children)
                directChildren.Add(child);
        }
    }
}
