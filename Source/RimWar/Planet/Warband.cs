﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    public class Warband : WarObject
    {
        private int lastEventTick = 0;
        private bool movesAtNight = false;
        private int ticksPerMove = 3300;
        private int searchTick = 60;
        private WorldObject targetWorldObject = null;
        private Settlement parentSettlement = null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.movesAtNight, "movesAtNight", false, false);
            Scribe_Values.Look<int>(ref this.lastEventTick, "lastEventTick", 0, false);
            Scribe_Values.Look<int>(ref this.ticksPerMove, "ticksPerMove", 3300, false);
            Scribe_References.Look<WorldObject>(ref this.targetWorldObject, "targetWorldObject");
            Scribe_References.Look<Settlement>(ref this.parentSettlement, "parentSettlement");
        }

        public override void Tick()
        {
            base.Tick();
            if(Find.TickManager.TicksGame % this.searchTick == 0)
            {
                //scan for nearby engagements
                this.searchTick = Rand.Range(300, 500);
                ScanForNearbyEnemy(1); //WorldUtility.GetRimWarDataForFaction(this.Faction).GetEngagementRange()
                if (this.DestinationTarget != null && this.DestinationTarget.Tile != pather.Destination)
                {
                    PathToTarget(this.DestinationTarget);
                }
                if (DestinationTarget is Warband || DestinationTarget is Caravan)
                {
                    EngageNearbyEnemy();
                }
                
            }
            if(Find.TickManager.TicksGame % 60 == 0)
            {
                if (this.parentSettlement == null)
                {
                    FindParentSettlement();                    
                }
                //target is gone; return home
                if (this.targetWorldObject == null)
                {
                    this.DestinationTarget = Find.World.worldObjects.WorldObjectAt(this.parentSettlement.Tile, WorldObjectDefOf.Settlement);
                    if (DestinationTarget != null && DestinationTarget.Tile != pather.Destination)
                    {
                        pather.StartPath(DestinationTarget.Tile, true, false);
                    }
                    else
                    {
                        //not heading in the right direction; pause then attempt to reroute
                        pather.StopDead();
                    }
                }                
            }
        }

        public void ScanForNearbyEnemy(int range)
        {
            List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(this.Tile, range);
            if (worldObjects != null && worldObjects.Count > 0)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];
                    if (wo.Faction != this.Faction && wo != this.targetWorldObject)
                    {
                        //Log.Message("" + this.Name + " scanned nearby object " + this.targetWorldObject.Label);
                        if (wo is Warband || wo is Caravan) //or rimwar caravan, or diplomat, or merchant; ignore scouts and settlements
                        {
                            //Log.Message(this.Label + " engaging nearby warband " + wo.Label);
                            this.DestinationTarget = worldObjects[i];                            
                            break;
                        }
                    }
                }
            }
        }

        public void PathToTarget(WorldObject wo)
        {
            pather.StartPath(wo.Tile, true);
            tweener.ResetTweenedPosToRoot();
        }

        public void EngageNearbyEnemy()
        {
            if(this.DestinationTarget != null && (this.DestinationTarget.Tile == this.Tile || Find.WorldGrid.TraversalDistanceBetween(this.Tile, this.DestinationTarget.Tile) <= 1))
            {
                ImmediateAction(this.DestinationTarget);
            }
            else if( this.DestinationTarget != null && Find.WorldGrid.TraversalDistanceBetween(this.Tile, this.DestinationTarget.Tile) <= 2)
            {
                PathToTarget(this.DestinationTarget);
            }
            else
            {
                this.DestinationTarget = null;

            }
                   
        }

        public Warband()
        {

        }

        public override int RimWarPoints { get => base.RimWarPoints; set => base.RimWarPoints = value; }

        public Settlement ParentSettlement
        {
            get
            {
                WorldObject wo = Find.World.worldObjects.WorldObjectAt(this.parentSettlement.Tile, WorldObjectDefOf.Settlement);
                if (wo != null && wo.Faction == this.Faction)
                {
                    return this.parentSettlement;
                }
                else
                {
                    this.parentSettlement = null;
                    return this.parentSettlement;
                }
                
            }
            set
            {
                this.parentSettlement = value;
            }
        }

        public bool MovesAtNight
        {
            get
            {
                return movesAtNight;
            }
            set
            {
                movesAtNight = value;
            }
        }

        public override bool NightResting
        {
            get
            {
                if (!base.Spawned)
                {
                    return false;
                }
                if (pather.Moving && pather.nextTile == pather.Destination && Caravan_PathFollower.IsValidFinalPushDestination(pather.Destination) && Mathf.CeilToInt(pather.nextTileCostLeft / 1f) <= 10000)
                {
                    return false;
                }
                if (movesAtNight)
                {
                    return !CaravanNightRestUtility.RestingNowAt(base.Tile);
                }
                return CaravanNightRestUtility.RestingNowAt(base.Tile);
            }
        }

        public override int TicksPerMove
        {
            get
            {
                return this.ticksPerMove;
            }
            set
            {
                this.ticksPerMove = value;
            }
        }

        public WorldObject DestinationTarget
        {
            get
            {
                return this.targetWorldObject;
            }
            set
            {
                this.targetWorldObject = value;
            }
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            //stringBuilder.Append(base.GetInspectString());            

            if (Find.World.worldObjects.AnySettlementAt(pather.Destination))
            {
                WorldObject wo = Find.World.worldObjects.ObjectsAt(pather.Destination).FirstOrDefault();
                if (wo.Faction != this.Faction)
                {
                    stringBuilder.Append("RW_WarbandInspectString".Translate(this.Name, "RW_Attacking".Translate(), wo.Label));
                }
                else
                {
                    stringBuilder.Append("RW_WarbandInspectString".Translate(this.Name, "RW_ReturningTo".Translate(), wo.Label));
                }
            }

            if (pather.Moving)
            {
                float num6 = (float)Utility.ArrivalTimeEstimator.EstimatedTicksToArrive(base.Tile, pather.Destination, this) / 60000f;
                if (stringBuilder.Length != 0)
                {
                    stringBuilder.AppendLine();
                }
                stringBuilder.Append("RW_EstimatedTimeToDestination".Translate(num6.ToString("0.#")));
                stringBuilder.Append("\n" + Find.WorldGrid.TraversalDistanceBetween(this.Tile, pather.Destination) + " tiles");
            }
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }
            stringBuilder.Append("RW_CombatPower".Translate(this.RimWarPoints));
            if (!pather.MovingNow)
            {

            }
            return stringBuilder.ToString();
        }

        public override void ImmediateAction(WorldObject wo)
        {
            if(wo != null)
            {
                if(wo.Faction != null && wo.Faction.HostileTo(this.Faction))
                {
                    if(wo is Warband)
                    {
                        IncidentUtility.ResolveWarbandBattle(this, wo as Warband);
                        base.ImmediateAction(wo);
                    }
                    else if(wo is Caravan)
                    {
                        IncidentUtility.DoCaravanAttackWithPoints(this.RimWarPoints, wo as Caravan, this.rimwarData, PawnsArrivalModeDefOf.EdgeWalkIn);
                    }
                }                
            }
            else
            {
                base.ImmediateAction(wo);
            }
            
        }

        public override void ArrivalAction()
        {
            //Log.Message("beginning arrival actions");
            WorldObject wo = Find.World.worldObjects.ObjectsAt(pather.Destination).FirstOrDefault();
            if(wo.Faction != this.Faction)
            {
                if (wo.Faction.HostileTo(this.Faction))
                {
                    if (wo.Faction == Faction.OfPlayer)
                    {
                        //Do Raid
                        RimWorld.Planet.Settlement playerSettlement = Find.World.worldObjects.SettlementAt(this.Tile);
                        Caravan playerCaravan = Find.World.worldObjects.PlayerControlledCaravanAt(this.Tile);
                        if (playerSettlement != null)
                        {
                            //Raid Player Map
                            IncidentUtility.DoRaidWithPoints(this.RimWarPoints, playerSettlement, WorldUtility.GetRimWarDataForFaction(this.Faction), PawnsArrivalModeDefOf.EdgeWalkIn);
                        }
                        else if (playerCaravan != null)
                        {
                            //Raid player caravan
                            IncidentUtility.DoCaravanAttackWithPoints(this.RimWarPoints, playerCaravan, this.rimwarData, PawnsArrivalModeDefOf.EdgeWalkIn);
                        }
                    }
                    else
                    {
                        Settlement settlement = WorldUtility.GetRimWarSettlementAtTile(this.Tile);
                        if (settlement != null)
                        {
                            IncidentUtility.ResolveWarbandAttackOnSettlement(this, this.ParentSettlement, settlement, WorldUtility.GetRimWarDataForFaction(this.Faction));
                        }
                        else if (wo is Warband)
                        {
                            IncidentUtility.ResolveWorldEngagement(this);
                        }
                    }                
                }
            }
            else
            {
                //Log.Message("this tile: " + this.Tile + " parent settlement tile: " + this.parentSettlement.Tile);
                if(this.Tile == parentSettlement.Tile)
                {
                    if(Find.World.worldObjects.AnyMapParentAt(this.Tile))
                    {
                        //reinforce
                        //Log.Message("attempting to reinforce");
                        //Log.Message("map is spawn " + Find.World.worldObjects.MapParentAt(this.Tile).Spawned);
                        //Log.Message("map " + Find.World.worldObjects.MapParentAt(this.Tile).Map + " has faction " + Find.World.worldObjects.MapParentAt(this.Tile).Faction);
                        this.parentSettlement.RimWarPoints += this.RimWarPoints;
                    }
                    else
                    {
                        //Log.Message("parent settlement points: " + this.parentSettlement.RimWarPoints);
                        if (wo.Faction != this.Faction) //could happen if parent town is taken over while army is away, in which case - perform another raid
                        {

                        }
                        this.parentSettlement.RimWarPoints += this.RimWarPoints;
                    }
                }
            }
            //Log.Message("ending arrival actions");
            base.ArrivalAction();
        }

        public void FindParentSettlement()
        {
            this.parentSettlement = WorldUtility.GetFriendlyRimWarSettlementsInRange(this.Tile, 20, this.Faction, WorldUtility.GetRimWarData(), WorldUtility.GetRimWarDataForFaction(this.Faction)).RandomElement();
            if(this.parentSettlement == null)
            {
                //expand search parameters
                WorldUtility.GetFriendlyRimWarSettlementsInRange(this.Tile, 200, this.Faction, WorldUtility.GetRimWarData(), WorldUtility.GetRimWarDataForFaction(this.Faction)).RandomElement();
                if (this.parentSettlement == null)
                {
                    //warband is lost, no nearby parent settlement
                    Find.WorldObjects.Remove(this);
                }
                else
                {
                    PathToTarget(Find.World.worldObjects.WorldObjectAt(this.parentSettlement.Tile, WorldObjectDefOf.Settlement));
                }
            }
            else
            {
                PathToTarget(Find.World.worldObjects.WorldObjectAt(this.parentSettlement.Tile, WorldObjectDefOf.Settlement));
            }
        }

        public void FindHostileSettlement()
        {
            this.DestinationTarget = Find.World.worldObjects.WorldObjectOfDefAt(WorldObjectDefOf.Settlement, WorldUtility.GetHostileRimWarSettlementsInRange(this.Tile, 20, this.Faction, WorldUtility.GetRimWarData(), WorldUtility.GetRimWarDataForFaction(this.Faction)).RandomElement().Tile);
            if (this.DestinationTarget != null)
            {
                PathToTarget(this.DestinationTarget);                
            }
            else
            {
                if (this.parentSettlement == null)
                {
                    FindParentSettlement();
                }
                else
                {
                    PathToTarget(Find.World.worldObjects.WorldObjectAt(this.parentSettlement.Tile, WorldObjectDefOf.Settlement));
                }
            }
        }        
    }
}
