﻿using System.Collections.Generic;
using JetBrains.Annotations;

namespace BCM.Models
{
  public class BCMCmdArea : BCCmd
  {
    public string Filter;
    public ushort Radius;

    public bool HasPos;
    public BCMVector3 Position;
    public BCMVector3 MaxPos => new BCMVector3(Position.x + Size.x - 1, Position.y + Size.y - 1, Position.z + Size.z - 1);

    public bool HasSize;
    public BCMVector3 Size;

    public bool HasChunkPos;
    public BCMVector4 ChunkBounds;
    public ItemStack ItemStack;

    [NotNull] public readonly Dictionary<string, string> Opts;
    [NotNull] public readonly List<string> Pars;
    [NotNull] public readonly string CmdType;

    public BCMCmdArea(string cmdType)
    {
      Opts = new Dictionary<string, string>();
      Pars = new List<string>();
      CmdType = cmdType;
    }

    public BCMCmdArea(List<string> pars, Dictionary<string, string> options, string cmdType)
    {
      Opts = options;
      Pars = pars;
      CmdType = cmdType;
    }

    public bool IsWithinBounds(Vector3i pos)
    {
      if (!HasPos) return false;

      var size = HasSize ? Size : new BCMVector3(1, 1, 1);
      return (Position.x <= pos.x && pos.x < Position.x + size.x) &&
             (Position.y <= pos.y && pos.y < Position.y + size.y) &&
             (Position.z <= pos.z && pos.z < Position.z + size.z);
    }
  }
}
