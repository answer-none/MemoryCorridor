using System;
using System.Collections.Generic;

namespace MemoryCorridor.Core
{
    /// <summary>音频与体验设置（本地持久化，V2 schema）。</summary>
    [Serializable]
    public class AppSettings
    {
        public float masterVolume = 1.0f;
        public float narrationVolume = 1.0f;
        public float ambientVolume = 0.6f;
        public bool showLightPoints = true;
        public bool showStarLabels = true; // R5：星点标签显隐（schema v2 扩展字段）
        public float panelDistance = 2.4f;  // R11 舒适性：面板距离（米，1.5-3.0）
        public bool vignetteEnabled = false; // R11 舒适性：舒适暗角开关
        public float vignetteIntensity = 0.3f; // R11 舒适性：暗角强度（0-0.4）
        public string locomotionMode = "teleport"; // R11 舒适性：teleport / smooth
        public string activeThemeId = "default"; // R17-1：运行时主题选择（default/rain 循环，切房保持用户选择）
        public int fadeStartDays = 30;
        public float fadeFloor = 0.25f;
    }

    /// <summary>应用根数据：成员 + 房间 + 设置 + 导出清单。</summary>
    [Serializable]
    public class AppData
    {
        public string appVersion = "0.2";
        public string currentMemberId = "mem_01";
        public AppSettings settings = new AppSettings();
        public List<MemberData> members = new List<MemberData>();
        public List<RoomData> rooms = new List<RoomData>();
        public ExportManifest exportManifest;
    }

    /// <summary>导出包自描述（导入时校验）。</summary>
    [Serializable]
    public class ExportManifest
    {
        public string exportedAt;
        public List<string> memberIds = new List<string>();
        public string contentHash;
    }
}
