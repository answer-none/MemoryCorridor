using System;
using System.Collections;
using System.IO;
using MemoryCorridor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryCorridor.Media
{
    /// <summary>
    /// 记忆面板（W4 UGUI 磨砂版 + R2 音频链路）：WorldSpace Canvas——暮色半透明底 +
    /// 琥珀描边 + 照片 RawImage + TMP 标题/正文 + ✕ 关闭钮 + 录音播放（E8）。
    /// 显示时定位于视线前方 1.5m 固定不随头（§5.3）；音量走 AudioSettingsService（E9）。
    /// </summary>
    public class MemoryPanel : MonoBehaviour
    {
        private Canvas canvas;
        private RawImage photoImage;
        private TMP_Text titleText;
        private TMP_Text bodyText;
        private Texture2D loadedTexture;
        private TMP_FontAsset fontAsset;
        private AudioSource audioSource;
        private Coroutine audioLoadRoutine;
        private UnityEngine.Video.VideoPlayer videoPlayer;
        private RenderTexture videoRT;
        private UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable closeInteractable;
        private CanvasGroup canvasGroup;
        private Coroutine fadeRoutine;

        public bool IsShowing => gameObject.activeSelf;
        public string TitleString => titleText != null ? titleText.text : null;
        public Texture PhotoTexture => photoImage != null ? photoImage.texture : null;
        public bool HasCloseButton => closeInteractable != null;
        public bool IsAudioPlaying => audioSource != null && audioSource.isPlaying;
        public bool IsVideoPlaying => videoPlayer != null && videoPlayer.isPlaying; // R8


        private void Awake()
        {
            EnsureBuilt();
        }

        /// <summary>按碎片内容刷新面板并显示于视线前方。</summary>
        public void Show(MemoryFragment fragment)
        {
            if (fragment == null) return;
            EnsureBuilt();
            PositionInFrontOfGaze();

            if (titleText != null)
            {
                titleText.text = string.IsNullOrEmpty(fragment.title) ? "未命名记忆" : fragment.title;
            }
            if (bodyText != null)
            {
                bodyText.text = (fragment.diary != null && !string.IsNullOrEmpty(fragment.diary.text))
                    ? fragment.diary.date + "\n" + fragment.diary.text
                    : "";
            }
            ShowFirstImage(fragment);
            StartVideo(fragment); // R8：视频优先（有 video 项则 RT 接管照片区）
            gameObject.SetActive(true); // 坑-036：先激活再 StartAudio——inactive 的 GameObject 上
            StartAudio(fragment);       // StartCoroutine 会静默失败

            // R4 F4：淡入（0.25s alpha 渐变）
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeIn(0.25f));
        }

        private IEnumerator FadeIn(float duration)
        {
            float t0 = Time.unscaledTime;
            while (canvasGroup != null && Time.unscaledTime - t0 < duration)
            {
                canvasGroup.alpha = Mathf.Clamp01((Time.unscaledTime - t0) / duration);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        /// <summary>隐藏面板（同时停止录音与视频）。</summary>
        public void Hide()
        {
            StopAudio();
            StopVideo();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// R8 视频记忆（坑-042 方案落地）：VideoPlayer + RenderTexture → RawImage，
        /// file:/// 直读 StreamingAssets；inactive 面板 Stop（坑-042③）。
        /// </summary>
        private void StartVideo(MemoryFragment fragment)
        {
            StopVideo();
            if (fragment.mediaItems == null) return;

            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "video" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved == null)
                {
                    Debug.LogWarning("[MemoryPanel] 视频缺失，降级为图片: "
                        + MediaResolver.DescribeMiss(item.path));
                    continue;
                }

                if (videoPlayer == null)
                {
                    videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
                    videoPlayer.playOnAwake = false;
                    videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
                    videoRT = new RenderTexture(1280, 720, 0);
                    videoPlayer.targetTexture = videoRT;
                    videoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;
                    videoPlayer.SetDirectAudioVolume(0, AudioSettingsService.NarrationVolume);
                }

                videoPlayer.url = "file:///" + resolved.Replace('\\', '/');
                videoPlayer.Play();
                if (photoImage != null)
                {
                    photoImage.texture = videoRT; // RawImage 直接显示 RenderTexture
                    photoImage.enabled = true;
                }
                return;
            }
        }

        private void StopVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        }


        private void EnsureBuilt()
        {
            if (canvas != null) return;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(620f, 460f);
            transform.localScale = Vector3.one * 0.0016f; // ≈1.0m × 0.74m

            // R3 美学升级：圆角底板（程序化贴图，UI_DESIGN §1 形状 tokens）
            Image background = gameObject.AddComponent<Image>();
            background.sprite = Sprite.Create(
                RoundedRectTexture.RoundedRect(620, 460, 24, UiTokens.PanelBg, UiTokens.Amber, 2),
                new Rect(0f, 0f, 620f, 460f), new Vector2(0.5f, 0.5f), 100f);
            background.raycastTarget = false;

            // R4 U1：照片圆角框（amber 描边底图，照片叠其上）
            GameObject photoFrameGo = new GameObject("PhotoFrame");
            photoFrameGo.transform.SetParent(transform, false);
            Image photoFrame = photoFrameGo.AddComponent<Image>();
            photoFrame.sprite = Sprite.Create(
                RoundedRectTexture.RoundedRect(560, 250, 20, new Color(0.16f, 0.13f, 0.19f, 1f), UiTokens.Amber, 2),
                new Rect(0f, 0f, 560f, 250f), new Vector2(0.5f, 0.5f), 100f);
            photoFrame.raycastTarget = false;
            Stretch(photoFrameGo.GetComponent<RectTransform>(), 0.045f, 0.435f, 0.955f, 0.945f);

            GameObject photoGo = new GameObject("Photo");
            photoGo.transform.SetParent(transform, false);
            photoImage = photoGo.AddComponent<RawImage>();
            photoImage.raycastTarget = false;
            Stretch(photoGo.GetComponent<RectTransform>(), 0.055f, 0.442f, 0.945f, 0.938f);

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();
            EnsureFont(titleText);
            titleText.fontSize = 42f;
            titleText.color = UiTokens.Amber;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;
            Stretch(titleGo.GetComponent<RectTransform>(), 0.03f, 0.30f, 0.97f, 0.42f);

            GameObject dividerGo = new GameObject("Divider");
            dividerGo.transform.SetParent(transform, false);
            Image divider = dividerGo.AddComponent<Image>();
            divider.color = new Color(UiTokens.Amber.r, UiTokens.Amber.g, UiTokens.Amber.b, 0.55f); // 标题分隔线（UI_DESIGN §1）
            divider.raycastTarget = false;
            Stretch(dividerGo.GetComponent<RectTransform>(), 0.04f, 0.295f, 0.96f, 0.302f);

            GameObject bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyText = bodyGo.AddComponent<TextMeshProUGUI>();
            EnsureFont(bodyText);
            bodyText.fontSize = 28f;
            bodyText.color = UiTokens.Cream;
            bodyText.alignment = TextAlignmentOptions.Top;
            bodyText.enableWordWrapping = true; // TMP 3.0.9：无 textWrappingMode（坑-029）
            bodyText.raycastTarget = false;
            Stretch(bodyGo.GetComponent<RectTransform>(), 0.05f, 0.04f, 0.95f, 0.28f);

            BuildCloseButton();
        }

        /// <summary>
        /// 关闭按钮（R1 E3）：canvas 右上角 3D ✕ + XRSimpleInteractable，射线点选即 Hide()。
        /// 挂在 canvas 子级需换算像素坐标/比例（root scale 0.0016）。
        /// </summary>
        private void BuildCloseButton()
        {
            if (closeInteractable != null) return;

            const float PixelScale = 0.0016f;
            GameObject buttonGo = new GameObject("CloseButton");
            buttonGo.transform.SetParent(transform, false);
            buttonGo.transform.localPosition = new Vector3(262f, 196f, -10f);
            buttonGo.transform.localScale = new Vector3(0.045f / PixelScale, 0.045f / PixelScale, 1f);

            MeshRenderer buttonRenderer = buttonGo.AddComponent<MeshRenderer>();
            Material buttonMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            buttonMat.SetColor("_BaseColor", new Color(0.5f, 0.3f, 0.3f, 1f));
            buttonRenderer.material = buttonMat;

            GameObject crossGo = new GameObject("Cross");
            crossGo.transform.SetParent(buttonGo.transform, false);
            crossGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            MeshRenderer crossRenderer = crossGo.AddComponent<MeshRenderer>();
            TextMesh crossTm = crossGo.AddComponent<TextMesh>();
            Font font = UiTextFactory.Legacy;
            crossTm.font = font;
            crossRenderer.sharedMaterial = font.material;
            crossTm.text = "×";
            crossTm.fontSize = 64;
            crossTm.characterSize = 0.02f;
            crossTm.anchor = TextAnchor.MiddleCenter;
            crossTm.alignment = TextAlignment.Center;
            crossTm.color = new Color(0.95f, 0.92f, 0.88f, 1f);

            closeInteractable = buttonGo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
            closeInteractable.selectEntered.AddListener(delegate (
                UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
            {
                Hide();
            });
        }

        private void EnsureFont(TMP_Text text)
        {
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    UiTextFactory.Legacy);
            }
            text.font = fontAsset;
        }

        private static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void PositionInFrontOfGaze()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.position = cam.transform.position + cam.transform.forward * 1.5f;
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            }
        }

        private void ShowFirstImage(MemoryFragment fragment)
        {
            if (photoImage == null) return;

            string imagePath = FindFirstImagePath(fragment);
            if (imagePath == null)
            {
                photoImage.texture = null;
                photoImage.enabled = false;
                return;
            }

            byte[] bytes = File.ReadAllBytes(imagePath);
            if (loadedTexture == null)
            {
                loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            }
            if (!loadedTexture.LoadImage(bytes))
            {
                Debug.LogWarning("[MemoryPanel] 图片解码失败: " + imagePath);
                photoImage.texture = null;
                photoImage.enabled = false;
                return;
            }

            photoImage.texture = loadedTexture;
            photoImage.enabled = true;
        }

        private static string FindFirstImagePath(MemoryFragment fragment)
        {
            if (fragment.mediaItems == null) return null;
            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "image" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved != null) return resolved;
                Debug.LogWarning("[MemoryPanel] 素材缺失: " + MediaResolver.DescribeMiss(item.path));
            }
            return null;
        }

        /// <summary>
        /// E8：碎片音频播放链路（WAV/MP3 从持久化或随包目录异步加载）。
        /// 文件缺失静默降级（日志）；音量走 AudioSettingsService.NarrationVolume。
        /// </summary>
        private void StartAudio(MemoryFragment fragment)
        {
            StopAudio();
            if (fragment.mediaItems == null) return;

            for (int i = 0; i < fragment.mediaItems.Count; i++)
            {
                MediaItem item = fragment.mediaItems[i];
                if (item.kind != "audio" || string.IsNullOrEmpty(item.path)) continue;
                string resolved = MediaResolver.ResolveExisting(item.path);
                if (resolved == null)
                {
                    Debug.LogWarning("[MemoryPanel] 音频缺失，静默降级: "
                        + MediaResolver.DescribeMiss(item.path));
                    continue;
                }

                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f;
                    audioSource.playOnAwake = false;
                }

                if (audioLoadRoutine != null) StopCoroutine(audioLoadRoutine);
                audioLoadRoutine = StartCoroutine(LoadAndPlayAudio(resolved));
                return; // 只播第一条音频
            }
        }

        private IEnumerator LoadAndPlayAudio(string resolvedPath)
        {
            string url = "file:///" + resolvedPath.Replace('\\', '/');
            AudioType type = resolvedPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                ? AudioType.MPEG
                : AudioType.WAV;
            using (UnityEngine.Networking.UnityWebRequest request =
                UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[MemoryPanel] 音频加载失败: " + request.error);
                    yield break;
                }
                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || audioSource == null) yield break;

                AudioSettingsService.WarmSettings();
                audioSource.clip = clip;
                audioSource.volume = AudioSettingsService.NarrationVolume;
                audioSource.Play();
            }
        }

        private void StopAudio()
        {
            if (audioLoadRoutine != null)
            {
                StopCoroutine(audioLoadRoutine);
                audioLoadRoutine = null;
            }
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
            }
        }
    }
}
