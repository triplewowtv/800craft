﻿using System;
using System.Linq;
using System.Text;
using fCraft.Events;
using fCraft.Physics;
using System.Threading;

namespace fCraft.Physics
{
    class WaterPhysics
    {
        public static Thread waterThread;
        public static Thread waterSpreadThread;

        public static void playerPlacedWater(object sender, Events.PlayerPlacedBlockEventArgs e)
        {
            try
            {
                if (e.NewBlock == Block.Water)
                {
                    World world = e.Player.World;
                    if (world.waterPhysics)
                    {
                        if (e.NewBlock == Block.Water)
                        {
                            world.waterQueue.TryAdd(e.Coords.ToString(), e.Coords);
                        }
                    }
                }
            }
            catch (Exception ex) {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void towerInit(object sender, Events.PlayerPlacedBlockEventArgs e)
        {
            try
            {
                World world = e.Player.World;
                if (e.Player.towerMode)
                {
                    if (world.Map != null && world.IsLoaded)
                    {
                        if (e.Context == BlockChangeContext.Manual)
                        {
                            if (e.NewBlock == Block.Iron)
                            {
                                waterThread = new Thread(new ThreadStart(delegate
                                {
                                    if (e.Player.TowerCache != null)
                                    {
                                        world.Map.QueueUpdate(new BlockUpdate(null, e.Player.towerOrigin, Block.Air));
                                        e.Player.towerOrigin = e.Coords;
                                        foreach (Vector3I block in e.Player.TowerCache.Values)
                                        {
                                            e.Player.Send(PacketWriter.MakeSetBlock(block, Block.Air));
                                        }
                                        e.Player.TowerCache.Clear();
                                    }
                                    e.Player.towerOrigin = e.Coords;
                                    for (int z = e.Coords.Z; z <= world.Map.Height; z++)
                                    {
                                        Thread.Sleep(Physics.Tick);
                                        if (world.Map.GetBlock(e.Coords.X, e.Coords.Y, z + 1) != Block.Air
                                            || world.Map.GetBlock(e.Coords) != Block.Iron
                                            || e.Player.towerOrigin != e.Coords
                                            || !e.Player.towerMode)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            Vector3I tower = new Vector3I(e.Coords.X, e.Coords.Y, z + 1);
                                            e.Player.TowerCache.TryAdd(tower.ToString(), tower);
                                            e.Player.Send(PacketWriter.MakeSetBlock(tower, Block.Water));
                                        }
                                    }
                                })); waterThread.Start();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void towerRemove(object sender, Events.PlayerClickingEventArgs e)
        {
            try
            {
                World world = e.Player.World;
                if (e.Action == ClickAction.Delete)
                {
                    if (e.Coords == e.Player.towerOrigin)
                    {
                        if (e.Player.TowerCache != null)
                        {
                            if (world.Map != null && world.IsLoaded)
                            {
                                if (e.Block == Block.Iron)
                                {
                                    e.Player.towerOrigin = new Vector3I();
                                    foreach (Vector3I block in e.Player.TowerCache.Values)
                                    {
                                        e.Player.Send(PacketWriter.MakeSetBlock(block, Block.Air));
                                    }
                                    e.Player.TowerCache.Clear();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void waterChecker(SchedulerTask task)
        {
            try
            {
                if (waterSpreadThread != null)
                {
                    if (waterSpreadThread.ThreadState != ThreadState.Stopped)
                    {
                        return;
                    }
                }
                waterSpreadThread = new Thread(new ThreadStart(delegate
                {
                    foreach (World world in WorldManager.Worlds)
                    {
                        if (world.IsLoaded && world.Map != null && world.waterPhysics && world.waterQueue.Count > 0)
                        {
                            Map map = world.Map;
                            if (world.waterQueue.Values.Count > 0)
                            {
                                foreach (Vector3I block in world.waterQueue.Values)
                                {
                                    Random rand = new Random();
                                    int spread = rand.Next(1, 36);
                                    if (world.Map != null && world.IsLoaded && world.waterPhysics)
                                    {
                                        if (map.GetBlock(block) == Block.Water)
                                        {
                                            if (world.Map.GetBlock(block.X, block.Y, block.Z - 1) != Block.Water &&
                                                world.Map.GetBlock(block.X, block.Y, block.Z - 1) != Block.Air)
                                            {
                                                waterCheck(block.X - 1, block.Y, block.Z, world);
                                                waterCheck(block.X + 1, block.Y, block.Z, world);
                                                waterCheck(block.X, block.Y - 1, block.Z, world);
                                                waterCheck(block.X, block.Y + 1, block.Z, world);
                                            }
                                            else
                                            {
                                                if (spread == 8) // 1 in 35
                                                {
                                                    if (world.Map.GetBlock(block.X, block.Y, block.Z - 1) == Block.Air)
                                                    {
                                                        waterCheck(block.X - 1, block.Y, block.Z, world);
                                                        waterCheck(block.X + 1, block.Y, block.Z, world);
                                                        waterCheck(block.X, block.Y - 1, block.Z, world);
                                                        waterCheck(block.X, block.Y + 1, block.Z, world);
                                                    }
                                                }
                                            }
                                            waterCheck(block.X, block.Y, block.Z - 1, world);
                                        }
                                        else
                                        {
                                            Vector3I removed;
                                            world.waterQueue.TryRemove(block.ToString(), out removed);
                                        }
                                    }
                                }
                            }
                        }
                    }

                })); waterSpreadThread.Start();
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }
        




        #region hidden
        /*
        public static void deQueueWater(SchedulerTask task)
        {
            if (waterQueueThread != null)
            {
                if (waterQueueThread.ThreadState != ThreadState.Stopped) //stops multiple threads from opening
                {
                    return;
                }
            }
            var worlds = WorldManager.Worlds.Where(w => w.IsLoaded && w.Map != null && w.waterPhysics);
            foreach (World world in worlds)
            {
                if (world.Map != null && world.IsLoaded)
                {
                    if (world.waterPhysics)
                    {
                        if (world.waterQueue.Values.Count > 0)
                        {
                            waterQueueThread = new Thread(new ThreadStart(delegate
                             {
                                 foreach (Vector3I block in world.waterQueue.Values)
                                 {
                                     if (world.IsLoaded && world.Map != null && world.waterPhysics)
                                     {
                                         if (world.Map.GetBlock(block) == Block.Water) //if block is still water
                                         {
                                             waterCheck(block.X - 1, block.Y, block.Z, world);
                                             waterCheck(block.X + 1, block.Y, block.Z, world);
                                             waterCheck(block.X, block.Y + 1, block.Z, world);
                                             waterCheck(block.X, block.Y - 1, block.Z, world);
                                             waterCheck(block.X, block.Y, block.Z - 1, world);
                                         }
                                         else
                                         {
                                             Vector3I removed;
                                             world.waterQueue.TryRemove(block.ToString(), out removed); //else remove it from collection
                                         }
                                     }
                                 }
                             })); waterQueueThread.Start();
                        }
                    }
                }
            }
        }
*/


        /*public static void deQueueWater(SchedulerTask task)
        {
            if (waterQueueThread != null)
            {
                if (waterQueueThread.ThreadState != ThreadState.Stopped && waterQueueThread.ThreadState == ThreadState.WaitSleepJoin) //stops multiple threads from opening
                {
                    return;
                }
            }
            var worlds = WorldManager.Worlds.Where(w => w.waterPhysics && w.IsLoaded && w.Map != null);
            foreach (World world in worlds)
            {
                if (world.waterPhysics)
                {
                    waterQueueThread = new Thread(new ThreadStart(delegate
                    {

                        if (world.waterQueue != null)
                        {
                            foreach (Vector3I block in world.waterQueue.Values)
                            {
                                if (world.Map.GetBlock(block.X, block.Y, block.Z) == Block.Water)
                                {
                                    if (world.Map.GetBlock(block.X, block.Y, block.Z - 1) != Block.Water &&
                                        world.Map.GetBlock(block.X, block.Y, block.Z - 1) != Block.Air)
                                    {
                                        if (world.waterPhysics)
                                        {
                                            waterCheck(block.X - 1, block.Y, block.Z, world);
                                            waterCheck(block.X + 1, block.Y, block.Z, world);
                                            waterCheck(block.X, block.Y - 1, block.Z, world);
                                            waterCheck(block.X, block.Y + 1, block.Z, world);
                                        }
                                    }
                                    else
                                    {
                                        waterCheck(block.X, block.Y, block.Z - 1, world);
                                    }
                                }
                            }
                        } Thread.Sleep(350);
                    })); waterQueueThread.Start();
                }
            }
        }
        
        */
        #endregion
        public static void waterCheck(int x, int y, int z, World world)
        {
            if (world.Map != null && world.IsLoaded && world.Map.InBounds(x,y,z) &&
               world.Map.GetBlock(x, y, z) == Block.Air)
            {
                world.waterQueue.TryAdd(new Vector3I(x, y, z).ToString(), new Vector3I(x, y, z));
                world.Map.QueueUpdate(new BlockUpdate(null, (short)x, (short)y, (short)z, Block.Water));
            }
        }

        public static void blockFloat(object sender, Events.PlayerPlacingBlockEventArgs e)
        {
            try
            {
                World world = e.Player.World;
                if (world.waterPhysics)
                {
                    if (e.Context == BlockChangeContext.Manual)
                    {
                        if (e.NewBlock == Block.TNT && world.tntPhysics)
                        {
                            return;
                        }
                        if (e.NewBlock == Block.Red && world.fireworkPhysics)
                        {
                            return;
                        }
                        if (Physics.CanFloat(e.NewBlock))
                        {
                            waterThread = new Thread(new ThreadStart(delegate
                            {
                                for (int z = e.Coords.Z; z <= world.Map.Height; z++)
                                {
                                    if (world.Map != null && world.IsLoaded)
                                    {
                                        if (world.Map.GetBlock(e.Coords.X, e.Coords.Y, z) != Block.Water)
                                        {
                                            break;
                                        }
                                        else if (world.Map.GetBlock(e.Coords.X, e.Coords.Y, z) == Block.Water)
                                        {
                                            Thread.Sleep(Physics.Tick);
                                            if (z - 1 != e.Coords.Z - 1)
                                            {
                                                e.Player.WorldMap.QueueUpdate(new BlockUpdate(null, (short)e.Coords.X, (short)e.Coords.Y, (short)(z - 1), Block.Water)); //remove when water physics is done
                                            }
                                            e.Player.WorldMap.QueueUpdate(new BlockUpdate
                                                (null, (short)e.Coords.X, (short)e.Coords.Y, (short)(z), e.NewBlock));
                                        }
                                    }
                                }
                            })); waterThread.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void blockSink(object sender, Events.PlayerPlacingBlockEventArgs e)
        {
            try
            {
                World world = e.Player.World;
                if (world.waterPhysics)
                {
                    if (e.Context == BlockChangeContext.Manual)
                    {
                        if (!Physics.CanFloat(e.NewBlock)
                            && e.NewBlock != Block.Air
                            && e.NewBlock != Block.Water
                            && e.NewBlock != Block.Lava
                            && e.NewBlock != Block.BrownMushroom
                            && e.NewBlock != Block.RedFlower
                            && e.NewBlock != Block.RedMushroom
                            && e.NewBlock != Block.YellowFlower
                            && e.NewBlock != Block.Plant)
                        {
                            if (world.waterPhysics)
                            {
                                waterThread = new Thread(new ThreadStart(delegate
                                {
                                    for (int z = e.Coords.Z; z >= 1; z--)
                                    {
                                        if (world.Map != null && world.IsLoaded)
                                        {
                                            if (world.Map.GetBlock(e.Coords.X, e.Coords.Y, z) == Block.Water)
                                            {
                                                Thread.Sleep(Physics.Tick);
                                                world.Map.QueueUpdate(new BlockUpdate
                                                    (null,
                                                    (short)e.Coords.X,
                                                    (short)e.Coords.Y,
                                                    (short)(z + 1),
                                                    Block.Water));

                                                world.Map.QueueUpdate(new BlockUpdate
                                                    (null,
                                                    (short)e.Coords.X,
                                                    (short)e.Coords.Y,
                                                    (short)(z),
                                                    e.NewBlock));
                                            }
                                        }
                                    }
                                })); waterThread.Start();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void drownCheck(SchedulerTask task)
        {
            try
            {
                foreach (Player p in Server.Players)
                {
                    if (p.World != null) //ignore console
                    {
                        if (p.World.waterPhysics)
                        {
                            Position pos = new Position(
                                (short)(p.Position.X / 32),
                                (short)(p.Position.Y / 32),
                                (short)((p.Position.Z + 1) / 32)
                            );
                            if (p.WorldMap.GetBlock(pos.X, pos.Y, pos.Z) == Block.Water)
                            {
                                if (p.DrownTime == null || (DateTime.Now - p.DrownTime).TotalSeconds > 33)
                                {
                                    p.DrownTime = DateTime.Now;
                                }
                                if ((DateTime.Now - p.DrownTime).TotalSeconds > 30)
                                {
                                    p.TeleportTo(p.WorldMap.Spawn);
                                    p.World.Players.Message("{0}&S drowned and died", p.ClassyName);
                                }
                            }
                            else
                            {
                                p.DrownTime = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }
    }
}