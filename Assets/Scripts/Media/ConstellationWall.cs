using System;
using System.Collections;
using System.Collections.Generic;
using MemoryCorridor.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 星河回顾墙（TECH_DESIGN_V2 §3.3 标志性画面）：走读终点，本房间碎片按路线顺序
    /// 在弧形墙上排成星座，发光细线相连；点星即重演该回忆。
    /// W3 版：星点=发光球体；缩略图/文案牌 W4 UGUI 替换。
    /// 坑-026：星点选择事件在生成时接线（Play 期），无编辑期订阅。
    /// </summary>
    public class ConstellationWall : MonoBehaviour
    {
        [SerializeField] private Material glowMaterial;
        [SerializeField] private MemoryPanel panel;
        [SerializeField] private ReplaySequencer sequencer;

        private readonly List<GameObject> stars = new List<GameObject>();
        private LineRenderer constellationLine;
        private System.Collections.Generic.List<MemoryFragment> currentRoute;

        public bool IsVisible { get; private set; }
        public int StarCount => stars.Count;

        /// <summary>星点被选中（点击/射线确认）事件。</summary>
        public event Action<string> OnStarSelected;

        /// <summary>编辑器搭建注入引用。</summary>
        public void Configure(Material glowRef, MemoryPanel panelRef, ReplaySequencer sequencerRef)
        {
            glowMaterial = glowRef;
            panel = panelRef;
            sequencer = sequencerRef;
        }

        /// <summary>按路线顺序建墙（在走读结束时由 RoutePlayer 调用）。</summary>
        public void Build(System.Collections.Generic.List<MemoryFragment> orderedFragments)
        {
            Clear();
            if (orderedFragments == null || orderedFragments.Count == 0) return;
            currentRoute = orderedFragments;
            IsVisible = true;

            Camera cam = Camera.main;
            Vector3 center = cam != null ? cam.transform.position : Vector3.zero;
            Vector3 forward = cam != null
                ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized
                : Vector3.forward;

            int n = orderedFragments.Count;
            const float Radius = 2.5f;
            const float Height = 1.4f;
            const float ArcDegrees = 60f;

            Vector3[] arcPositions = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float angle = (n > 1) ? (-ArcDegrees * 0.5f + ArcDegrees * i / (n - 1)) : 0f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Vector3 pos = center + dir * Radius;
                pos.y = Height;
                arcPositions[i] = pos;

                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "Star_" + orderedFragments[i].id;
                star.transform.position = pos;
                star.transform.localScale = Vector3.one * 0.08f;
                MeshRenderer starRenderer = star.GetComponent<MeshRenderer>();
                if (glowMaterial != null) starRenderer.sharedMaterial = glowMaterial;

                StarNode node = star.AddComponent<StarNode>();
                node.FragmentId = orderedFragments[i].id;
                node.OnSelected += OnStarPicked;

                XRSimpleInteractable interactable = star.AddComponent<XRSimpleInteractable>();
                StarNode captured = node;
                interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
                {
                    captured.Select();
                });
                stars.Add(star);
            }

            GameObject lineGo = new GameObject("ConstellationLine");
            lineGo.transform.SetParent(transform, false);
            constellationLine = lineGo.AddComponent<LineRenderer>();
            constellationLine.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                constellationLine.SetPosition(i, arcPositions[i]);
            }
            constellationLine.startWidth = 0.015f;
            constellationLine.endWidth = 0.015f;
            constellationLine.useWorldSpace = true;
            if (glowMaterial != null) constellationLine.material = glowMaterial;

            Debug.Log("[ConstellationWall] 星河墙生成：" + n + " 颗星");
        }

        /// <summary>清墙（下次走读开始时）。</summary>
        public void Clear()
        {
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] != null) Destroy(stars[i]);
            }
            stars.Clear();
            if (constellationLine != null)
            {
                Destroy(constellationLine.gameObject);
                constellationLine = null;
            }
            currentRoute = null;
            IsVisible = false;
        }

        private void OnStarPicked(string fragmentId)
        {
            if (OnStarSelected != null) OnStarSelected(fragmentId);
            if (panel == null || currentRoute == null) return;

            for (int i = 0; i < currentRoute.Count; i++)
            {
                if (currentRoute[i].id != fragmentId) continue;
                panel.Show(currentRoute[i]);
                if (sequencer != null)
                {
                    sequencer.Play(currentRoute[i], transform.position);
                }
                break;
            }
        }
    }
}
