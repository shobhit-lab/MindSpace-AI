using System;
using System.Collections.Generic;

namespace MindSpace
{
    [Serializable] public class CardDto { public string id, text, author, groupLabel; public float x, y, z; public int group; public List<string> votes = new(); }
    [Serializable] public class GroupDto { public string label; public List<string> ids = new(); }
    [Serializable] public class TopDto { public string id, text, why; }
    [Serializable] public class AiDto { public string source; public List<GroupDto> groups = new(); public List<TopDto> top = new(); public List<string> plan = new(); }
    [Serializable] public class StateDto { public int version; public bool unchanged; public string decision; public List<CardDto> cards = new(); public AiDto ai; }
    [Serializable] public class CardPost { public string id, text, author; public float x, y, z; }
    [Serializable] public class VotePost { public string id, voter; }
}
