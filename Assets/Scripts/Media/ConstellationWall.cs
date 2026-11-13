using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private bool labelsVisible = true;

        public bool IsVisible { get; private set; }
        public int StarCount => stars.Count;

        /// <summary>R5：星点标签显隐（底板+文字一起隐）。</summary>
        public void SetLabelsVisible(bool visible)
        {
            labelsVisible = visible;
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] == null) continue;
                Transform label = stars[i].transform.Find("Label");
                if (label != null) label.gameObject.SetActive(visible);
            }
        }

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

                // 缩略图：图片碎片贴小卡片；音/日记星保持纯光球（W4 §3.3）
                Texture2D thumbTexture = LoadFirstImageTexture(orderedFragments[i]);
                if (thumbTexture != null)
                {
                    GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    thumb.name = "Thumb";
                    Collider thumbCollider = thumb.GetComponent<Collider>();
                    if (thumbCollider != null) Destroy(thumbCollider);
                    thumb.transform.SetParent(star.transform, false);
                    thumb.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                    thumb.transform.localScale = new Vector3(1.6f, 1.2f, 1f);
                    MeshRenderer thumbRenderer = thumb.GetComponent<MeshRenderer>();
                    Material thumbMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    thumbMat.mainTexture = thumbTexture;
                    thumbRenderer.material = thumbMat;
                }

                XRSimpleInteractable interactable = star.AddComponent<XRSimpleInteractable>();
                StarNode captured = node;
                interactable.selectEntered.AddListener(delegate (SelectEnterEventArgs args)
                {
                    captured.Select();
                });

                // R2 E10 → R4 U4：星点标签底板化（圆角贴图+描边，可读性）
                GameObject labelGo = new GameObject("Label");
                labelGo.transform.SetParent(star.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                labelGo.transform.localScale = new Vector3(1.7f, 0.55f, 1f);
                MeshRenderer labelBoardRenderer = labelGo.AddComponent<MeshRenderer>();
                Shader spriteShader2 = Shader.Find("MemoryCorridor/Sprite3D");
                Material labelBoardMat = new Material(spriteShader2);
                labelBoardMat.mainTexture = RoundedRectTexture.RoundedRect(
                    280, 72, 14, new Color(0.10f, 0.08f, 0.13f, 0.9f),
                    new Color(0.91f, 0.64f, 0.29f), 2);
                labelBoardMat.SetColor("_Color", Color.white);
                labelBoardRenderer.material = labelBoardMat;

                GameObject labelTextGo = new GameObject("LabelText");
                labelTextGo.transform.SetParent(labelGo.transform, false);
                labelTextGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                labelTextGo.transform.localScale = new Vector3(0.62f, 1.8f, 1f);
                MeshRenderer labelRenderer = labelTextGo.AddComponent<MeshRenderer>();
                TextMesh labelTm = labelTextGo.AddComponent<TextMesh>();
                Font font = UiTextFactory.Legacy;
                labelTm.font = font;
                labelRenderer.sharedMaterial = font.material;
                labelTm.text = orderedFragments[i].title;
                labelTm.fontSize = 40;
                labelTm.characterSize = 0.028f;
                labelTm.anchor = TextAnchor.MiddleCenter;
                labelTm.alignment = TextAlignment.Center;
                labelTm.color = new Color(0.92f, 0.88f, 0.82f, 1f);
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
            if (currentRoute == null) return;

            for (int i = 0; i < currentRoute.Count; i++)
            {
                if (currentRoute[i].id != fragmentId) continue;
                // R2：面板显示统一由桥接器的重演节拍回调驱动（wall 路径同样延迟出面板）；
                // 此处只发起星点位置的重演。
                if (sequencer != null)
                {
                    // R1 E6：重演发生在被点星的位置（而非墙中心）
                    Vector3 starPosition = (i < stars.Count && stars[i] != null)
                        ? stars[i].transform.position
                        : transform.position;
                    sequencer.Play(currentRoute[i], starPosition, "wall");
                }
                break;
            }
        }

        private static Texture2D LoadFirstImageTexture(MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return null;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved == null) continue;
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(File.ReadAllBytes(resolved))) return texture;
            }
            return null;
        }
    }
}
