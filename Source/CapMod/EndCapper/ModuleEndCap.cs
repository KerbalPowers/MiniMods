using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EndCapper
{
    // This module is applied to parts with multiple possible jettison shrouds to allow automatic triggers
    public class ModuleEndCap : PartModule
    {
        [KSPField]
        public string nodeNames;         // "node_stack_top,node_stack_bottom"

        [KSPField]
        public string showAttached;      // "TopCap1;TopCap2,BottomCap1;BottomCap2"  

        [KSPField]
        public string showFree;          // "TopFree1;TopFree2,BottomFree1"

        [KSPEvent(guiActive = true,
                   guiActiveEditor = true,
                   guiName = "#LOC_KPDynamics_Capping")]
        public void EventToggleTracking() => ToggleCapping();

        [KSPField(isPersistant = true)]
        public bool cappingEnabled = true;

        // Internally handle as a nodeData object //TODO: Config structure change to defined nodes
        private class NodeData
        {
            public AttachNode node;
            public List<Transform> showWhenAttached = new List<Transform>();
            public List<Transform> showWhenFree = new List<Transform>();

            public NodeData(AttachNode node)
            {
                this.node = node;
            }
        }

        private List<NodeData> nodes = new List<NodeData>();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartEvent.Add(OnEditorEvent);
            }

            CacheInitialChildren();
            ParseConfig();
            UpdateVisuals(); 
            
            //Set starting value
            Events["EventToggleTracking"].guiName = cappingEnabled ? "#LOC_KPDynamics_DisableCapping" : "#LOC_KPDynamics_EnableCapping";
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onEditorPartEvent.Remove(OnEditorEvent);
            }
        }

        // Read the config file to find the node associations
        private void ParseConfig()
        {
            nodes.Clear();
            List<AttachNode> attachNodes = part.attachNodes;

            String test = attachNodes[0].id;

            var nodeList = nodeNames.Split(',');
            var attachedList = showAttached.Split(',');
            var freeList = showFree.Split(',');

            for (int i = 0; i < nodeList.Length; i++)
            {
                string nodeId = nodeList[i].Trim();
                AttachNode node = part.FindAttachNode(nodeId);
                Debug.Log(node != null
                    ? $"[ModuleEndCap] Found node '{nodeId}' for part '{part.name}'"
                    : $"[ModuleEndCap] WARNING: Node '{nodeId}' not found on part '{part.name}'");
                if (node == null) continue;

                // Create node data
                NodeData nodeData = new NodeData(node);

                // Attached transforms
                if (i < attachedList.Length && !string.IsNullOrWhiteSpace(attachedList[i]))
                {
                    foreach (var tName in attachedList[i].Split(';'))
                    {
                        Transform t = part.FindModelTransform(tName.Trim());
                        if (t != null) nodeData.showWhenAttached.Add(t);
                        else Debug.LogWarning($"[ModuleEndCap] Could not find attached transform '{tName}' on {part.name}");
                    }
                }

                // Free transforms
                if (i < freeList.Length && !string.IsNullOrWhiteSpace(freeList[i]))
                {
                    foreach (var tName in freeList[i].Split(';'))
                    {
                        Transform t = part.FindModelTransform(tName.Trim());
                        if (t != null) nodeData.showWhenFree.Add(t);
                        else Debug.LogWarning($"[ModuleEndCap] Could not find free transform '{tName}' on {part.name}");
                    }
                }

                nodes.Add(nodeData);
            }
        }

        private void UpdateVisuals()
        {
            foreach (var nodeData in nodes)
            {
                if (nodeData.node == null)
                {
                    Debug.LogWarning("[ModuleEndCap] NodeData has null node!");
                    continue;
                }

                bool cappingActive = cappingEnabled && nodeData.node.attachedPart != null;
                //Debug.Log($"[ModuleEndCap] Node '{nodeData.node.id}' attached? {attached}");

                // Show model transforms
                SetAttachedTransforms(!cappingActive, nodeData);
                SetFreeTransforms(cappingActive, nodeData);
            }
        }

        private void SetFreeTransforms(bool s, NodeData n)
        {
            // Show attached transforms
            foreach (var t in n.showWhenAttached)
            {
                if (t == null)
                {
                    Debug.LogWarning($"[ModuleEndCap] showWhenAttached transform null for node '{n.node.id}'");
                    continue;
                }
                t.gameObject.SetActive(s);
            }
        }

        private void SetAttachedTransforms(bool s, NodeData n)
        {
            // Show free transforms
            foreach (var t in n.showWhenFree)
            {
                if (t == null)
                {
                    Debug.LogWarning($"[ModuleEndCap] showWhenFree transform null for node '{n.node.id}'");
                    continue;
                }
                t.gameObject.SetActive(s);
            }
        }

        private void ToggleCapping()
        {
            cappingEnabled = !cappingEnabled; 
            Events["EventToggleTracking"].guiName = cappingEnabled ? "#LOC_KPDynamics_DisableCapping" : "#LOC_KPDynamics_EnableCapping";
            UpdateVisuals();
        }

        private HashSet<Part> directChildren = new HashSet<Part>();
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

            bool wasDirectChild = directChildren.Contains(p);
            bool isDirectChildNow = p.parent == part;

            // Only care about events involving this parts direct relatives
            switch (evt)
            {
                case ConstructionEventType.PartAttached:
                    if (isDirectChildNow)
                    {
                        directChildren.Add(p);
                        UpdateVisuals();
                    }
                    break;

                case ConstructionEventType.PartDetached:
                    if (wasDirectChild)
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
            directChildren.Clear();
            foreach (var child in part.children)
            {
                directChildren.Add(child);
            }
        }
    }
}
