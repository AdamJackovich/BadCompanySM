using JetBrains.Annotations;
using UnityEngine;

namespace BCM.Commands
{
  [UsedImplicitly]
  public class BCTeleport : BCCommandAbstract
  {
    protected override void Process()
    {
      if (!BCUtils.CheckWorld(out var world)) return;

      if (Params.Count == 0)
      {
        SendOutput(GetHelp());

        return;
      }

      switch (Params[0])
      {
        case "entity":
          {
            TeleportEntity(world, Params[1]);
          }
          break;

        default:
          TeleportEntity(world, Params[0]);

          return;
      }
    }

    //todo: add teleport in facing direction x meters
    private static bool GetPos(World world, out Vector3 position)
    {
      position = new Vector3(0, 0, 0);

      switch (Params.Count)
      {
        case 5:
          {
            if (!int.TryParse(Params[2], out var x) || !int.TryParse(Params[3], out var y) ||
                !int.TryParse(Params[4], out var z))
            {
              SendOutput("Unable to parse x y z for numbers");

              return false;
            }
            position = new Vector3(x, y, z);
            return true;
          }
        case 4:
          {
            if (!int.TryParse(Params[1], out var x) || !int.TryParse(Params[2], out var y) ||
                !int.TryParse(Params[3], out var z))
            {
              SendOutput("Unable to parse x y z for numbers");

              return false;
            }
            position = new Vector3(x, y, z);
            return true;
          }
        default:
          if (Options.ContainsKey("player"))
          {
            ConsoleHelper.ParseParamPartialNameOrId(Options["player"], out string _, out var ci);
            if (ci == null)
            {
              SendOutput("Unable to get player position from remote client");

              return false;
            }

            var p = world.Players.dict[ci.entityId]?.position;
            if (p == null)
            {
              SendOutput("Unable to get player position from client entity");

              return false;
            }

            position = (Vector3)p;
            return true;
          }
          else if (Options.ContainsKey("p"))
          {
            var pos = Options["p"].Split(',');
            if (pos.Length != 3)
            {
              SendOutput($"Unable to get position from {Options["p"]}, incorrect number of co-ords: /p=x,y,z");

              return false;
            }
            if (!int.TryParse(pos[0], out var x) ||
                !int.TryParse(pos[1], out var y) ||
                !int.TryParse(pos[2], out var z)
            )
            {
              SendOutput($"Unable to get x y z for {Options["p"]}");

              return false;
            }
            position = new Vector3(x, y, z);
            return true;
          }
          else if (Options.ContainsKey("position"))
          {
            var pos = Options["position"].Split(',');
            if (pos.Length != 3)
            {
              SendOutput($"Unable to get position from '{Options["position"]}', incorrect number of co-ords: /position=x,y,z");

              return false;
            }
            if (!int.TryParse(pos[0], out var x) ||
                !int.TryParse(pos[1], out var y) ||
                !int.TryParse(pos[2], out var z)
            )
            {
              SendOutput($"Unable to get x y z from '{Options["position"]}'");

              return false;
            }

            position = new Vector3(x, y, z);
            return true;
          }
          else if (SenderInfo.RemoteClientInfo != null)
          {
            var ci = SenderInfo.RemoteClientInfo;
            if (ci == null)
            {
              SendOutput("Unable to get player position from remote client");

              return false;
            }

            var p = world.Players.dict[ci.entityId]?.position;
            if (p == null)
            {
              SendOutput("Unable to get player position from client entity");

              return false;
            }

            position = (Vector3)p;
            return true;
          }
          return false;
      }
    }

    private static bool GetEntity(World world, string eid, out Entity entity)
    {
      entity = null;
      if (!int.TryParse(eid, out var entityId)) return false;

      entity = world.Entities.dict.ContainsKey(entityId)
        ? world.Entities.dict[entityId]
        : null;

      return true;
    }

    private static bool GetClientInfo(string eid, out ClientInfo ci) => (ci = ConsoleHelper.ParseParamIdOrName(eid)) != null;

    private static void TeleportEntity(World world, string param)
    {
      if (!GetPos(world, out var position)) return;

      if (GetClientInfo(param, out var ci))
      {
        ci.SendPackage(new NetPackageTeleportPlayer(position));
        SendOutput($"Teleporting {ci.playerName} to {position.x} {position.y} {position.z}");
      }
      else if (GetEntity(world, param, out var entity))
      {
        if (entity == null)
        {
          SendOutput("Entity not found'");

          return;
        }

        entity.SetPosition(position);
        SendOutput($"Teleporting Entity: {(entity is EntityAlive ea ? ea.EntityName : entity.entityType.ToString())} to {position.x} {position.y} {position.z}");
      }
    }
  }
}
