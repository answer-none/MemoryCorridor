using System;
using System.Collections.Generic;

namespace MemoryCorridor.Core
{
    /// <summary>光点/碎片位姿（Euler 角存储，兼容 V1 schema）。</summary>
    [Serializable]
    public class FragmentAnchor
    {
        public float[] position = { 0f, 1.2f, -1.5f };
        public float[] rotation = { 0f, 0f, 0f };

        public UnityEngine.Vector3 GetPosition()
        {
            return new UnityEngine.Vector3(position[0], position[1], position[2]);
        }

        public UnityEngine.Quaternion GetRotation()
        {
            return UnityEngine.Quaternion.Euler(rotation[0], rotation[1], rotation[2]);
        }
    }

    /// <summary>分层伪 3D 三层贴图路径（可空）。</summary>
    [Serializable]
    public class FragmentLayers
    {
        public string foreground;
        public string midground;
        public string background;
    }

    /// <summary>组合记忆内单条媒体项。</summary>
    [Serializable]
    public class MediaItem
    {
        public string kind;   // image / audio / video
        public string path;   // 相对 PersistentDataPath
        public string caption;
    }

    /// <summary>日记条目。</summary>
    [Serializable]
    public class DiaryEntry
    {
        public string date;  // 原始记忆日期（非保存时间）
        public string text;
    }

    /// <summary>记忆碎片：本项目核心数据单元（V2 schema）。</summary>
    [Serializable]
    public class MemoryFragment
    {
        public string id;
        public string title;
        public string type = "image"; // image / audio / video / diary / mixed
        public string ownerMemberId;
        public string roomId;
        public int order;
        public string visibility = "public"; // public / private
        public string createdAt;             // ISO8601
        public string lastVisitedAt;         // ISO8601，褪色机制依据（V2 新增）
        public DiaryEntry diary;
        public List<MediaItem> mediaItems = new List<MediaItem>();
        public FragmentAnchor anchor = new FragmentAnchor();
        public FragmentLayers layers;

        public FragmentType GetFragmentType()
        {
            switch (type)
            {
                case "image": return FragmentType.Image;
                case "audio": return FragmentType.Audio;
                case "video": return FragmentType.Video;
                case "diary": return FragmentType.Diary;
                case "mixed": return FragmentType.Mixed;
                default: return FragmentType.Image;
            }
        }
    }

    public enum FragmentType
    {
        Image,
        Audio,
        Video,
        Diary,
        Mixed
    }
}
