using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 分层伪 3D（TECH_DESIGN_V2 §3.2 第②拍）：单张记忆拆前景/中景/背景三层平面，
    /// 沿深度错开排列，头部/相机移动时按层视差位移，形成"可环绕的小场景"。
    /// W1 冒烟版仅基础视差；转场时序由 ReplaySequencer（W3）驱动。
    /// </summary>
    public class LayeredPhoto : MonoBehaviour
    {
        [SerializeField] private float layerSpacing = 0.35f;
        [SerializeField] private float parallaxStrength = 0.35f;
        [SerializeField] private Vector2 cardSize = new Vector2(1.2f, 0.9f);

        private Transform foreground;
        private Transform midground;
        private Transform background;
        private Vector3 baseCenter;
        private Camera headCamera;

        /// <summary>用三层贴图构建分层场景（可为 null，缺层自动跳过）。</summary>
        public void Build(Texture2D fg, Texture2D mg, Texture2D bg)
        {
            ClearLayers();
            baseCenter = transform.position;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

            if (bg != null) background = CreateLayer("Layer_BG", bg, unlit, layerSpacing * 2f);
            if (mg != null) midground = CreateLayer("Layer_MG", mg, unlit, layerSpacing);
            if (fg != null) foreground = CreateLayer("Layer_FG", fg, unlit, 0f);
        }

        private Transform CreateLayer(string layerName, Texture2D tex, Shader shader, float zOffset)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = layerName;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, zOffset);
            quad.transform.localScale = new Vector3(cardSize.x, cardSize.y, 1f);

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            Material mat = new Material(shader);
            mat.mainTexture = tex;
            renderer.material = mat;
            return quad.transform;
        }

        private void ClearLayers()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            foreground = null;
            midground = null;
            background = null;
        }

        private void Update()
        {
            if (foreground == null && midground == null && background == null) return;
            if (headCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                headCamera = cam;
            }

            Vector3 delta = headCamera.transform.position - baseCenter;
            if (foreground != null)
                foreground.localPosition = new Vector3(delta.x, delta.y, 0f) * parallaxStrength;
            if (midground != null)
                midground.localPosition = new Vector3(delta.x, delta.y, layerSpacing) * parallaxStrength * 0.5f;
            if (background != null)
                background.localPosition = new Vector3(delta.x, delta.y, layerSpacing * 2f) * parallaxStrength * 0.25f;
        }
    }
}
