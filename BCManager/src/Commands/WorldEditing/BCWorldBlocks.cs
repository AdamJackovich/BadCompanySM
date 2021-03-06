using System;
using System.Collections.Generic;
using BCM.Models;
using JetBrains.Annotations;

namespace BCM.Commands
{
  //todo: use Chunk.RecalcHeightAt(int _x, int _yMaxStart, int _z)
  //todo: allow /y=-1 too be used to offset the insert when using player locs to allow sub areas to be accessed without underground clipping
  //      /y=terrain to set the bottom y co-ord to the lowest non terrain block -1
  //todo: apply a custom placeholder mapping to a block value in the area
  //todo: refactor to use CmdArea and tasks

  [UsedImplicitly]
  public class BCWorldBlocks : BCCommandAbstract
  {
    protected override void Process()
    {
      if (!BCUtils.CheckWorld(out var world)) return;

      if (Options.ContainsKey("undo"))
      {
        SendOutput("Please use the bc-undo command to undo changes");

        return;
      }

      var pos1 = new Vector3i(int.MinValue, 0, int.MinValue);
      var pos2 = new Vector3i(int.MinValue, 0, int.MinValue);
      string blockname;
      string blockname2 = null;

      //get loc and player current pos
      EntityPlayer sender = null;
      string steamId = null;
      if (SenderInfo.RemoteClientInfo != null)
      {
        steamId = SenderInfo.RemoteClientInfo.ownerId;
        sender = world.Entities.dict[SenderInfo.RemoteClientInfo.entityId] as EntityPlayer;
        if (sender != null)
        {
          pos2 = new Vector3i((int)Math.Floor(sender.serverPos.x / 32f), (int)Math.Floor(sender.serverPos.y / 32f), (int)Math.Floor(sender.serverPos.z / 32f));
        }
        else
        {
          SendOutput("Error: unable to get player location");

          return;
        }
      }

      switch (Params.Count)
      {
        case 2:
        case 3:
          if (steamId != null)
          {
            pos1 = BCLocation.GetPos(steamId);
            if (pos1.x == int.MinValue)
            {
              SendOutput("No location stored. Use bc-loc to store a location.");

              return;
            }

            blockname = Params[1];
            if (Params.Count == 3)
            {
              blockname2 = Params[2];
            }
          }
          else
          {
            SendOutput("Error: unable to get player location");

            return;
          }
          break;
        case 8:
        case 9:
          //parse params
          if (!int.TryParse(Params[1], out pos1.x) || !int.TryParse(Params[2], out pos1.y) || !int.TryParse(Params[3], out pos1.z) || !int.TryParse(Params[4], out pos2.x) || !int.TryParse(Params[5], out pos2.y) || !int.TryParse(Params[6], out pos2.z))
          {
            SendOutput("Error: unable to parse coordinates");

            return;
          }
          blockname = Params[7];
          if (Params.Count == 9)
          {
            blockname2 = Params[8];
          }
          break;
        default:
          SendOutput("Error: Incorrect command format.");
          SendOutput(GetHelp());

          return;
      }

      var size = BCUtils.GetSize(pos1, pos2);

      var position = new Vector3i(
        pos1.x < pos2.x ? pos1.x : pos2.x,
        pos1.y < pos2.y ? pos1.y : pos2.y,
        pos1.z < pos2.z ? pos1.z : pos2.z
      );

      //**************** GET BLOCKVALUE
      var bvNew = int.TryParse(blockname, out var blockId) ? Block.GetBlockValue(blockId) : Block.GetBlockValue(blockname);

      var modifiedChunks = BCUtils.GetAffectedChunks(new BCMCmdArea("Blocks")
      {
        Position = new BCMVector3(position),
        Size = new BCMVector3(size),
        HasPos = true,
        HasSize = true,

        ChunkBounds = new BCMVector4
        {
          x = World.toChunkXZ(position.x),
          y = World.toChunkXZ(position.z),
          z = World.toChunkXZ(position.x + size.x - 1),
          w = World.toChunkXZ(position.z + size.z - 1)
        },
        HasChunkPos = true
      }, world);

      //CREATE UNDO
      //create backup of area blocks will insert to
      if (!Options.ContainsKey("noundo"))
      {
        BCUtils.CreateUndo(sender, position, size);
      }

      switch (Params[0])
      {
        case "scan":
          ScanBlocks(position, size, bvNew, blockname);
          break;
        case "fill":
          FillBlocks(position, size, bvNew, blockname, modifiedChunks);
          break;
        case "swap":
          SwapBlocks(position, size, bvNew, blockname2, modifiedChunks);
          break;
        case "repair":
          RepairBlocks(position, size, modifiedChunks);
          break;
        case "damage":
          DamageBlocks(position, size, modifiedChunks);
          break;
        case "upgrade":
          UpgradeBlocks(position, size, modifiedChunks);
          break;
        case "downgrade":
          DowngradeBlocks(position, size, modifiedChunks);
          break;
        case "paint":
          SetPaint(position, size, modifiedChunks);
          break;
        case "paintface":
          SetPaintFace(position, size, modifiedChunks);
          break;
        case "paintstrip":
          RemovePaint(position, size, modifiedChunks);
          break;
        case "density":
          SetDensity(position, size, modifiedChunks);
          break;
        case "rotate":
          SetRotation(position, size, modifiedChunks);
          break;
        case "meta1":
          SetMeta(1, position, size, modifiedChunks);
          break;
        case "meta2":
          SetMeta(2, position, size, modifiedChunks);
          break;
        case "meta3":
          SetMeta(3, position, size, modifiedChunks);
          break;
        default:
          SendOutput(GetHelp());
          break;
      }
    }

    private static void SetDensity(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      sbyte density = 1;
      if (Options.ContainsKey("d"))
      {
        if (sbyte.TryParse(Options["d"], out density))
        {
          SendOutput($"Using density {density}");
        }
      }

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air) || worldBlock.ischild) continue;

            GameManager.Instance.World.ChunkClusters[clrIdx]
              .SetBlock(worldPos, false, worldBlock, true, density, false, false, Options.ContainsKey("force"));
            counter++;
          }
        }
      }

      SendOutput($"Setting density on {counter} blocks '{density}' @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void SetMeta(int metaIdx, Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      if (!byte.TryParse(Options["meta"], out var meta))
      {
        SendOutput($"Unable to parse meta '{Options["meta"]}'");

        return;
      }

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air) || worldBlock.ischild) continue;

            switch (metaIdx)
            {
              case 1:
                worldBlock.meta = meta;
                break;
              case 2:
                worldBlock.meta2 = meta;
                break;
              case 3:
                worldBlock.meta3 = meta;
                break;
              default:
                return;
            }

            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, worldBlock, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Setting meta{metaIdx} on '{counter}' blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void SetRotation(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      byte rotation = 0;
      if (Options.ContainsKey("rot"))
      {
        if (!byte.TryParse(Options["rot"], out rotation))
        {
          SendOutput($"Unable to parse rotation '{Options["rot"]}'");

          return;
        }
      }

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air) || worldBlock.ischild || !worldBlock.Block.shape.IsRotatable) continue;

            worldBlock.rotation = rotation;
            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, worldBlock, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Setting rotation on '{counter}' blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void DowngradeBlocks(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            var downgradeBlockValue = worldBlock.Block.DowngradeBlock;
            if (downgradeBlockValue.Equals(BlockValue.Air) || worldBlock.ischild) continue;

            downgradeBlockValue.rotation = worldBlock.rotation;
            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, downgradeBlockValue, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Downgrading {counter} blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void UpgradeBlocks(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            var upgradeBlockValue = worldBlock.Block.UpgradeBlock;
            if (upgradeBlockValue.Equals(BlockValue.Air) || worldBlock.ischild) continue;

            upgradeBlockValue.rotation = worldBlock.rotation;
            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, upgradeBlockValue, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Upgrading {counter} blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void DamageBlocks(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      var damageMin = 0;
      var damageMax = 0;
      if (Options.ContainsKey("d"))
      {
        if (Options["d"].IndexOf(",", StringComparison.InvariantCulture) > -1)
        {
          var dRange = Options["d"].Split(',');
          if (dRange.Length != 2)
          {
            SendOutput("Unable to parse damage values");

            return;
          }

          if (!int.TryParse(dRange[0], out damageMin))
          {
            SendOutput("Unable to parse damage min value");

            return;
          }

          if (!int.TryParse(dRange[1], out damageMax))
          {
            SendOutput("Unable to parse damage max value");

            return;
          }
        }
        else
        {
          if (!int.TryParse(Options["d"], out damageMin))
          {
            SendOutput("Unable to parse damage value");

            return;
          }
        }
      }

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air)) continue;

            var max = worldBlock.Block.blockMaterial.MaxDamage;
            var damage = (damageMax != 0 ? UnityEngine.Random.Range(damageMin, damageMax) : damageMin) + worldBlock.damage;
            if (Options.ContainsKey("nobreak"))
            {
              worldBlock.damage = Math.Min(damage, max - 1);
            }
            else if (Options.ContainsKey("overkill"))
            {
              while (damage >= max)
              {
                var downgradeBlock = worldBlock.Block.DowngradeBlock;
                damage -= max;
                max = downgradeBlock.Block.blockMaterial.MaxDamage;
                downgradeBlock.rotation = worldBlock.rotation;
                worldBlock = downgradeBlock;
              }
              worldBlock.damage = damage;
            }
            else
            {
              //needs to downgrade if damage > max, no overflow damage
              if (damage >= max)
              {
                var downgradeBlock = worldBlock.Block.DowngradeBlock;
                downgradeBlock.rotation = worldBlock.rotation;
                worldBlock = downgradeBlock;
              }
              else
              {
                worldBlock.damage = damage;
              }
            }

            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, worldBlock, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Damaging {counter} blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void RepairBlocks(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air)) continue;

            worldBlock.damage = 0;
            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlock(worldPos, worldBlock, false, false);
            counter++;
          }
        }
      }

      SendOutput($"Repairing {counter} blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void SetPaintFace(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      byte texture = 0;
      if (Options.ContainsKey("t"))
      {
        if (!byte.TryParse(Options["t"], out texture))
        {
          SendOutput("Unable to parse texture value");

          return;
        }
        if (BlockTextureData.list[texture] == null)
        {
          SendOutput($"Unknown texture index {texture}");

          return;
        }
      }
      uint setFace = 0;
      if (Options.ContainsKey("face"))
      {
        if (!uint.TryParse(Options["face"], out setFace))
        {
          SendOutput("Unable to parse face value");

          return;
        }
      }
      if (setFace > 5)
      {
        SendOutput("Face must be between 0 and 5");

        return;
      }

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Equals(BlockValue.Air)) continue;

            GameManager.Instance.World.ChunkClusters[clrIdx].SetBlockFaceTexture(worldPos, (BlockFace)setFace, texture);
            counter++;
          }
        }
      }

      SendOutput($"Painting {counter} blocks on face '{((BlockFace)setFace).ToString()}' with texture '{BlockTextureData.GetDataByTextureID(texture)?.Name}' @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void SetPaint(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      var texture = 0;
      if (Options.ContainsKey("t"))
      {
        if (!int.TryParse(Options["t"], out texture))
        {
          SendOutput("Unable to parse texture value");

          return;
        }
        if (BlockTextureData.list[texture] == null)
        {
          SendOutput($"Unknown texture index {texture}");

          return;
        }
      }

      var num = 0L;
      for (var face = 0; face < 6; face++)
      {
        var num2 = face * 8;
        num &= ~(255L << num2);
        num |= (long)(texture & 255) << num2;
      }
      var textureFull = num;

      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Block.shape.IsTerrain() || worldBlock.Equals(BlockValue.Air)) continue;

            GameManager.Instance.World.ChunkClusters[clrIdx].SetTextureFull(worldPos, textureFull);
            counter++;
          }
        }
      }

      SendOutput($"Painting {counter} blocks with texture '{BlockTextureData.GetDataByTextureID(texture)?.Name}' @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void RemovePaint(Vector3i position, Vector3i size, Dictionary<long, Chunk> modifiedChunks)
    {
      const int clrIdx = 0;
      var counter = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.Block.shape.IsTerrain() || worldBlock.Equals(BlockValue.Air)) continue;

            GameManager.Instance.World.ChunkClusters[clrIdx].SetTextureFull(worldPos, 0L);
            counter++;
          }
        }
      }

      SendOutput($"Paint removed from {counter} blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void SwapBlocks(Vector3i position, Vector3i size, BlockValue newbv, string blockname, Dictionary<long, Chunk> modifiedChunks)
    {
      var targetbv = int.TryParse(blockname, out var blockId) ? Block.GetBlockValue(blockId) : Block.GetBlockValue(blockname);

      var block1 = Block.list[targetbv.type];
      if (block1 == null)
      {
        SendOutput("Unable to find target block by id or name");

        return;
      }

      var block2 = Block.list[newbv.type];
      if (block2 == null)
      {
        SendOutput("Unable to find replacement block by id or name");

        return;
      }

      const int clrIdx = 0;
      var counter = 0;
      var world = GameManager.Instance.World;
      for (var i = 0; i < size.x; i++)
      {
        for (var j = 0; j < size.y; j++)
        {
          for (var k = 0; k < size.z; k++)
          {
            sbyte density = 1;
            if (Options.ContainsKey("d"))
            {
              if (sbyte.TryParse(Options["d"], out density))
              {
                SendOutput($"Using density {density}");
              }
            }
            else
            {
              if (newbv.Equals(BlockValue.Air))
              {
                density = MarchingCubes.DensityAir;
              }
              else if (newbv.Block.shape.IsTerrain())
              {
                density = MarchingCubes.DensityTerrain;
              }
            }

            var textureFull = 0L;
            if (Options.ContainsKey("t"))
            {
              if (!byte.TryParse(Options["t"], out var texture))
              {
                SendOutput("Unable to parse texture index");

                return;
              }

              if (BlockTextureData.list[texture] == null)
              {
                SendOutput($"Unknown texture index {texture}");

                return;
              }

              var num = 0L;
              for (var face = 0; face < 6; face++)
              {
                var num2 = face * 8;
                num &= ~(255L << num2);
                num |= (long)(texture & 255) << num2;
              }
              textureFull = num;
            }

            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            if (world.GetBlock(worldPos).Block.GetBlockName() != block1.GetBlockName()) continue;

            world.ChunkClusters[clrIdx].SetBlock(worldPos, true, newbv, false, density, false, false);
            world.ChunkClusters[clrIdx].SetTextureFull(worldPos, textureFull);
            counter++;
          }
        }
      }

      SendOutput($"Replaced {counter} '{block1.GetBlockName()}' blocks with '{block2.GetBlockName()}' @ {position} to {BCUtils.GetMaxPos(position, size)}");
      SendOutput("Use bc-undo to revert the changes");
      Reload(modifiedChunks);
    }

    private static void FillBlocks(Vector3i position, Vector3i size, BlockValue bv, string search, Dictionary<long, Chunk> modifiedChunks)
    {
      const int clrIdx = 0;

      if (Block.list[bv.type] == null)
      {
        SendOutput("Unable to find block by id or name");

        return;
      }

      SetBlocks(clrIdx, position, size, bv, search == "*");

      if (Options.ContainsKey("delmulti"))
      {
        SendOutput($"Removed multidim blocks @ {position} to {BCUtils.GetMaxPos(position, size)}");
      }
      else
      {
        SendOutput($"Inserting block '{Block.list[bv.type].GetBlockName()}' @ {position} to {BCUtils.GetMaxPos(position, size)}");
        SendOutput("Use bc-undo to revert the changes");
      }

      Reload(modifiedChunks);
    }

    private static void SetBlocks(int clrIdx, Vector3i position, Vector3i size, BlockValue bvNew, bool searchAll)
    {
      var world = GameManager.Instance.World;
      var chunkCluster = world.ChunkClusters[clrIdx];

      sbyte density = 1;
      if (Options.ContainsKey("d"))
      {
        if (sbyte.TryParse(Options["d"], out density))
        {
          SendOutput($"Using density {density}");
        }
      }

      var textureFull = 0L;
      if (Options.ContainsKey("t"))
      {
        if (!byte.TryParse(Options["t"], out var texture))
        {
          SendOutput("Unable to parse texture index");

          return;
        }

        if (BlockTextureData.list[texture] == null)
        {
          SendOutput($"Unknown texture index {texture}");

          return;
        }

        var num = 0L;
        for (var face = 0; face < 6; face++)
        {
          var num2 = face * 8;
          num &= ~(255L << num2);
          num |= (long)(texture & 255) << num2;
        }
        textureFull = num;
      }

      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(position.x + i, position.y + j, position.z + k);
            var worldBlock = world.GetBlock(worldPos);

            if (Options.ContainsKey("delmulti") && (!searchAll || bvNew.type != worldBlock.type)) continue;

            //REMOVE PARENT OF MULTIDIM
            if (worldBlock.Block.isMultiBlock && worldBlock.ischild)
            {
              var parentPos = worldBlock.Block.multiBlockPos.GetParentPos(worldPos, worldBlock);
              var parent = chunkCluster.GetBlock(parentPos);
              if (parent.ischild || parent.type != worldBlock.type) continue;

              chunkCluster.SetBlock(parentPos, BlockValue.Air, false, false);
            }
            if (Options.ContainsKey("delmulti")) continue;

            //REMOVE LCB's
            if (worldBlock.Block.IndexName == "lpblock")
            {
              GameManager.Instance.persistentPlayers.RemoveLandProtectionBlock(new Vector3i(worldPos.x, worldPos.y, worldPos.z));
            }

            //todo: move to a chunk request and then process all blocks on that chunk
            var chunkSync = world.GetChunkFromWorldPos(worldPos.x, worldPos.y, worldPos.z) as Chunk;

            if (bvNew.Equals(BlockValue.Air))
            {
              density = MarchingCubes.DensityAir;

              if (world.GetTerrainHeight(worldPos.x, worldPos.z) > worldPos.y)
              {
                chunkSync?.SetTerrainHeight(worldPos.x & 15, worldPos.z & 15, (byte)worldPos.y);
              }
            }
            else if (bvNew.Block.shape.IsTerrain())
            {
              density = MarchingCubes.DensityTerrain;

              if (world.GetTerrainHeight(worldPos.x, worldPos.z) < worldPos.y)
              {
                chunkSync?.SetTerrainHeight(worldPos.x & 15, worldPos.z & 15, (byte)worldPos.y);
              }
            }
            else
            {
              //SET TEXTURE
              world.ChunkClusters[clrIdx].SetTextureFull(worldPos, textureFull);
            }

            //SET BLOCK
            world.ChunkClusters[clrIdx].SetBlock(worldPos, true, bvNew, true, density, false, false);
          }
        }
      }
    }

    private static void ScanBlocks(Vector3i position, Vector3i size, BlockValue bv, string search)
    {
      var block1 = Block.list[bv.type];
      if (block1 == null && search != "*")
      {
        SendOutput("Unable to find block by id or name");

        return;
      }

      var stats = new SortedDictionary<string, int>();
      const int clrIdx = 0;
      for (var j = 0; j < size.y; j++)
      {
        for (var i = 0; i < size.x; i++)
        {
          for (var k = 0; k < size.z; k++)
          {
            var worldPos = new Vector3i(i + position.x, j + position.y, k + position.z);
            var worldBlock = GameManager.Instance.World.GetBlock(clrIdx, worldPos);
            if (worldBlock.ischild) return;
            //var d = GameManager.Instance.World.GetDensity(_clrIdx, p5);
            //var t = GameManager.Instance.World.GetTexture(i + p3.x, j + p3.y, k + p3.z);
            var name = ItemClass.list[worldBlock.type]?.Name;
            if (string.IsNullOrEmpty(name))
            {
              name = "air";
            }

            if (search == "*")
            {
              SetStats(name, worldBlock, stats);
            }
            else
            {
              if (name != bv.Block.GetBlockName()) continue;

              SetStats(name, worldBlock, stats);
            }
          }
        }
      }

      SendJson(stats);
    }

    private static void SetStats(string name, BlockValue bv, IDictionary<string, int> stats)
    {
      if (stats.ContainsKey($"{bv.type:D4}:{name}"))
      {
        stats[$"{bv.type:D4}:{name}"] += 1;
      }
      else
      {
        stats.Add($"{bv.type:D4}:{name}", 1);
      }
    }

    private static void Reload(Dictionary<long, Chunk> modifiedChunks)
    {
      if (!(Options.ContainsKey("noreload") || Options.ContainsKey("nr")))
      {
        BCChunks.ReloadForClients(modifiedChunks);
      }
    }
  }
}
