using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 星尘粒子场（TECH_DESIGN_V2 §4.2）：跟随相机的暖色尘埃微粒，密度 200、
    /// 单 draw call，营造"暮色空气感"。W4 程序化 Shuriken；VFX Graph 升级列为可选。
    /// </summary>
    public class StardustField : MonoBehaviour
    {
        [SerializeField] private int maxParticles = 200;

        private ParticleSystem particles;

        public bool IsEmitting => particles != null && particles.isEmitting;
        public int MaxParticles => maxParticles;

        private void Awake()
        {
            particles = gameObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = maxParticles;
            main.startLifetime = 9f;
            main.startSpeed = 0.04f;
            main.startSize = 0.02f;
            main.startColor = new Color(0.91f, 0.72f, 0.45f, 0.55f); // 暖琥珀尘埃
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.loop = true;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 40f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 2.5f, 4f); // 相机周围 2m 半径感受区

            // 坑-038：无贴图粒子渲染成方块——换径向光晕贴图 + 透明 shader（圆形柔光）
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            Shader spriteShader = Shader.Find("MemoryCorridor/Sprite3D");
            Material mat = new Material(spriteShader);
            mat.mainTexture = RoundedRectTexture.RadialGlow(
                64, new Color(0.91f, 0.72f, 0.45f, 0.85f), 2.5f);
            mat.SetColor("_Color", Color.white);
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position;
            }
        }
    }
}
