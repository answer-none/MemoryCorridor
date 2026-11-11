using System;

namespace MemoryCorridor.Core
{
    /// <summary>成员档案：V2 简化版（名字选择，无头像上传）。</summary>
    [Serializable]
    public class MemberData
    {
        public string id;
        public string name = "我";
        public string role = "owner"; // owner / guest
    }
}
