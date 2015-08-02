﻿/*Copyright (c) 2014, Flip van Toly
 All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are 
permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of 
conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of 
conditions and the following disclaimer in the documentation and/or other materials provided with 
the distribution.

3. Neither the name of the copyright holder nor the names of its contributors may be used to 
endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS 
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY 
AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY 
OF SUCH DAMAGE.*/

//Namespace Declaration 
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Contracts;

namespace CommercialOfferings
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public partial class RMMModule : PartModule
    {
        /*[KSPField(isPersistant = true, guiActive = false)]
        public bool disablefunctionality = false;*/

        [KSPField(isPersistant = false, guiActive = false)]
        public bool DevMode = false;

        System.IO.DirectoryInfo GamePath;
        string CommercialOfferingsPath = "/GameData/RoutineMissionManager/";

        List<Offering> OfferingsList = new List<Offering>();

        //current mission
        [KSPField(isPersistant = true, guiActive = false)]
        public bool missionUnderway = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public string missionFolderName = "";
        [KSPField(isPersistant = true, guiActive = false)]
        public float missionArrivalTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int missionCrewCount = 0;
        private Offering missionOffering = new Offering();
        [KSPField(isPersistant = true, guiActive = false)]
        public bool missionRepeat = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public int missionRepeatDelay = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public string missionPreferedCrew = "";

        //values to save orbit
        [KSPField(isPersistant = true, guiActive = false)]
        public float SMAsave = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public float ECCsave = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public float INCsave = 0;


        //arrival transaction 
        private double nextLogicTime = 0;
        public bool completeArrival = false;
        private int ArrivalStage = 0;
        Vessel transactionVessel = null;
        private string tempID = "";

        static System.Random rand = new System.Random();

        //Port Code
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Port Code", guiUnits = "")]
        public string PortCode = "";

        //GUI
        private static GUIStyle windowStyle, labelStyle, redlabelStyle, textFieldStyle, buttonStyle;

        [KSPField(isPersistant = true, guiActive = false)]
        public bool OrderingEnabled = true;

        //GUI Main
        private static Rect windowPosGUIMain = new Rect(200, 200, 200, 450);
        private Vector2 scrollPositionAvailableCommercialOfferings;
        public float windowGUIMainX = 1;
        public float windowGUIMainY = 1;
        public float windowGUIMainWidth = 10;

        //public bool boolMissionRepeat = false;
        //public string strMissionRepeatDelay = "";

        Offering selectedOffering = new Offering();

        //GUI Offering
        private static Rect windowPosGUIOffering = new Rect(300, 100, 100, 100);
        Offering GUIOffering = new Offering();

        //GUI Order
        private static Rect windowPosGUIMission = new Rect(500, 400, 300, 75);
        Offering GUIMission = new Offering();
        private int intCrewCount = 0;
        private string strCrewCount = "";
        //private string strGUIerrmess = "";

        //GUI Pref Crew
        private static Rect windowPosGUIPrefCrew = new Rect(700, 200, 200, 600);
        List<ProtoCrewMember> preferredCrewList = new List<ProtoCrewMember>();
        private Vector2 scrollPositionPreferredCrew;
        private Vector2 scrollPositionAvailableCrew;

        //GUI Register Port
        private static Rect windowPosRegister = new Rect(300, 300, 240, 100);
        public string StrPortCode = "";

        //commercialvehiclemode
        [KSPField(isPersistant = true, guiActive = false)]
        public bool commercialvehiclemode = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public bool vehicleAutoDepart = false;
        [KSPField(isPersistant = true, guiActive = false)]
        public string commercialvehicleFolderName = "";
        [KSPField(isPersistant = true, guiActive = false)]
        public float commercialvehiclePartCount = 0.0f;
        private Offering commercialvehicleOffering = new Offering();
        private bool commercialvehicleOfferingLoaded = false;

        public override void OnAwake()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //print("waking");
                GamePath = System.IO.Directory.GetParent(Application.dataPath);
                if (part != null){ part.force_activate(); }
                ArrivalStage = 0;
                nextLogicTime = Planetarium.GetUniversalTime();

                if (DevMode) { OrderingEnabled = true; }
            }
        }

        public override void OnFixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }
            setModule();
            if (nextLogicTime == 0 || nextLogicTime > Planetarium.GetUniversalTime()) { return; }
            if (vessel.packed || !vessel.loaded)
            {
                nextLogicTime = Planetarium.GetUniversalTime();
                return;
            }

            //if (!moduleSet) { setModule(); moduleSet = true; }

            if (trackingActive || trackingPrimary)
            {
                if (!handleTracking()) {return; }
            }
            else
            {
                if (!handleCommercialVehicleMode()) { return; }
                handleArrivalCompletion();
            }
        }

        private void setModule()
        {
//            if (OrderingEnabled && vessel.situation == Vessel.Situations.ORBITING && (vessel.mainBody.name == "Kerbin" || vessel.mainBody.name == "Mun" || vessel.mainBody.name == "Minmus"))
//                Events["ordering"].guiActive = true;
//            else
//                Events["ordering"].guiActive = false;

            if (commercialvehiclemode && commercialvehicleOfferingLoaded)
            {
                Events["setAutoDepart"].guiActive = true;
            }
            else
            {
                Events["setAutoDepart"].guiActive = false;
            }

            if ((vessel.situation == Vessel.Situations.PRELAUNCH && !trackingActive) || (returnMission && checkDocked()))
            {
//                Events["ordering"].guiActive = false;
                Events["tracking"].guiActive = true;
            }
            else
            {
                Events["tracking"].guiActive = false;
            }

            if (PortCode == "" && vessel.situation == Vessel.Situations.ORBITING && bodyAllowed())
                Events["register"].guiActive = true;
            else
                Events["register"].guiActive = false;
        }

        private bool handleCommercialVehicleMode()
        {
            if (commercialvehiclemode && !commercialvehicleOfferingLoaded && commercialvehicleFolderName != "")
            {
                loadOfferings();

                foreach (Offering Off in OfferingsList)
                {
                    if (commercialvehicleFolderName == Off.folderName)
                    {
                        commercialvehicleOffering = Off;
                        commercialvehicleOfferingLoaded = true;
                        return false;
                    }
                }

                //commercialvehicleOffering.folderName = commercialvehicleFolderName;
                //if (File.Exists(GamePath + CommercialOfferingsPath + "/Offerings/" + commercialvehicleOffering.folderName + "/info.txt"))
                //{
                //    commercialvehicleOffering.loadOffering(GamePath + CommercialOfferingsPath + "/Offerings/" + commercialvehicleOffering.folderName + "/info.txt");
                //}
                //else
                //{
                //    print("Commercial Offerings Plugin unable to load " + commercialvehicleOffering.folderName);
                //    return;
                //}
                return (true);
            }

            if (commercialvehiclemode)
            {
                if (vehicleAutoDepart && !checkDocked())
                {
                    if (!vessel.isActiveVessel)
                    {
                        handleAutoDepart(); return false;
                    }
                    else
                    {
                        foreach (Vessel ves in FlightGlobals.Vessels)
                        {
                            if (!ves.packed && ves.loaded && ves.id.ToString() != vessel.id.ToString())
                            {
                                FlightGlobals.SetActiveVessel(ves);
                                nextLogicTime = Planetarium.GetUniversalTime() + 1;
                                return false;
                            }
                        }
                        vehicleAutoDepart = false;
                        nextLogicTime = 0;
                        ScreenMessages.PostScreenMessage("no other asset in vicinity", 4, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }
            return true;
        }


        private void handleArrivalCompletion()
        {
            if (ArrivalStage == 0)
            {
                if (missionUnderway && missionArrivalTime < Planetarium.GetUniversalTime())
                {
                    if (!otherModulesCompletingArrival())
                    {
                        //print(part.flightID + "executing");
                        ArrivalStage = 1;
                        completeArrival = true;
                        nextLogicTime = Planetarium.GetUniversalTime();
                    }
                    else
                    {
                        //print(part.flightID + "waiting");
                        ArrivalStage = 0;
                        nextLogicTime = Planetarium.GetUniversalTime() + 1.25;
                    }
                }
                else
                {
                    ArrivalStage = -1;
                    nextLogicTime = 0;
                }
            }
            if (completeArrival)
            {
                switch (ArrivalStage)
                {
                    case 1:
                        dockStage1();
                        break;
                    case 2:
                        dockStage2();
                        break;
                    case 3:
                        dockStage3();
                        break;
                    case 4:
                        if (transactionVessel == null || vessel.packed || !vessel.loaded || transactionVessel.packed || !transactionVessel.loaded)
                        {
                            nextLogicTime = Planetarium.GetUniversalTime();
                            return;
                        }
                        dockStage4();
                        break;
                }
            }
        }

        private void dockStage1()
        {
            //print("stage1 " + Planetarium.GetUniversalTime());


            loadOfferings();

            //print(missionFolderName);
            foreach (Offering Off in OfferingsList)
            {
                //print(Off.folderName);
                if (missionFolderName == Off.folderName)
                {
                    missionOffering = Off;
                    //print("got ehm");
                }
            }
            if (missionOffering == null) { abortArrival(); return; }

            //load Offering of current mission
            //missionOffering = new Offering();
            //missionOffering.folderName = missionFolderName;
            //if (File.Exists(GamePath + CommercialOfferingsPath + "/Offerings/" + missionOffering.folderName + "/info.txt"))
            //{
            //    missionOffering.loadOffering(GamePath + CommercialOfferingsPath + "/Offerings/" + missionOffering.folderName + "/info.txt");
            //}

            if (offeringAllowed(missionOffering) && crewAvailable(missionOffering))
            {
                nextLogicTime = Planetarium.GetUniversalTime();
                ArrivalStage = 2;
            }
            else
            {
                Funding.Instance.AddFunds(missionOffering.Price, TransactionReasons.VesselRecovery);
                missionUnderway = false;
                completeArrival = false;

                nextLogicTime = 0;
                ArrivalStage = -1;
            }
        }

        private void dockStage2()
        {
            //print("stage2 " + Planetarium.GetUniversalTime());
            toMapView();
            ProtoVessel ProtoFlightVessel = loadVessel(missionFolderName);
            if (ProtoFlightVessel == null) { abortArrival(); return; }
            if (loadVesselForRendezvous(ProtoFlightVessel, vessel))
            {
                nextLogicTime = Planetarium.GetUniversalTime();
                ArrivalStage = 3;
            }
        }

        private void dockStage3()
        {
            //search for the vessel for five seconds, else abort
            if (nextLogicTime < (Planetarium.GetUniversalTime() - 5)) { logreport(); abortArrival(); return; }

            foreach (Vessel ve in FlightGlobals.Vessels)
            {
                if (ve.vesselName == tempID)
                {
                    //print(ve.vesselName);
                    transactionVessel = ve;
                    transactionVessel.vesselName = missionOffering.VehicleName;
                    placeVesselForRendezvous(transactionVessel, vessel);
                    nextLogicTime = Planetarium.GetUniversalTime();
                    ArrivalStage = 4;
                    return;
                }
            }
        }

        private void logreport()
        {
            print("i'm at stage " + ArrivalStage + " and can not find vessel " + tempID + ". the vessels i can find are:");
            foreach (Vessel ve in FlightGlobals.Vessels)
            {
                print(ve.vesselName);
            }
            print("--test run");
            foreach (Vessel ve in FlightGlobals.Vessels)
            {
                if (ve.vesselName == tempID)
                {
                    print("test 1");
                    transactionVessel = ve;
                    print("test 2");
                    transactionVessel.vesselName = missionOffering.VehicleName;
                    print("test 3");
                    placeVesselForRendezvous(transactionVessel, vessel);
                    print("test 4");
                    nextLogicTime = Planetarium.GetUniversalTime();
                    ArrivalStage = 4;
                    return;
                }
            }
        }


        private void dockStage4()
        {
            //print("stage3 " + Planetarium.GetUniversalTime());
            toMapView();
            Part placePort = new Part();

            int portNumber = 0;
            //print("mis port " + missionOffering.Port);
            foreach (Part p in transactionVessel.parts)
            {
                foreach (PartModule pm in p.Modules)
                {
                    if (pm.ClassName == "ModuleDockingNode")
                    {
                        //print("portnum " + portNumber);
                        RMMModule ComOffMod = p.Modules.OfType<RMMModule>().FirstOrDefault();
                        //ComOffMod.disablefunctionality = true;
                        if (ComOffMod.trackingPrimary == true)
                        {
                            //print("yo " + portNumber);
                            placePort = p;
                            if (missionOffering.ReturnEnabled)
                            {
                                ComOffMod.commercialvehiclemode = true;
                                ComOffMod.commercialvehicleFolderName = missionOffering.folderName;
                                ComOffMod.commercialvehiclePartCount = (float)countVesselParts(transactionVessel);
                                ComOffMod.trackingPrimary = false;
                            }
                        }
                        portNumber = portNumber + 1;

                        ComOffMod.trackingActive = false;
                        ComOffMod.returnMission = false;
                        ComOffMod.trackID = "";
                        ComOffMod.PortCode = "";
                    }
                }
            }

            transactionVessel.targetObject = null;
//            ModuleDockingNode dockingPort = part.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
//
//            print(dockingPort.dockedPartUId.ToString());
//
//            //print(vessel.vesselName + " " + (dockingPort.state.Length >= 6 && dockingPort.state.Substring(0, 6) == "Docked" && null != dockingPort.vesselInfo.name));
//            if (dockingPort.state.Length >= 6 && dockingPort.state.Substring(0, 6) == "Docked" && null != dockingPort.vesselInfo.name)
//                print("ori" + true);
//
//            if (dockingPort.dockedPartUId == 0) { print("is zero" + true); }
//
//            foreach (Part p in vessel.parts)
//            {
//                if (p.flightID == dockingPort.dockedPartUId)
//                {
//                    print("found");
//                    foreach (PartModule pm in p.Modules)
//                    {
//                        if (pm.ClassName == "ModuleDockingNode")
//                        {
//                            //print("portnum " + portNumber);
//                            ModuleDockingNode joinedDockingPort = p.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
//                            if (part.flightID == joinedDockingPort.dockedPartUId)
//                            {
//                                if (joinedDockingPort.state.Length >= 6 && joinedDockingPort.state.Substring(0, 6) == "Docked" && null != joinedDockingPort.vesselInfo.name)
//                                    print("joi" + true); ;
//                            }
//                        }
//                    }
//                }
//            }
            handleLoadCrew(transactionVessel, missionCrewCount, missionOffering.MinimumCrew);
            handleContracts(transactionVessel, true, false);
            if (!checkDocked() && checkDockingPortCompatibility(placePort, part))
            {
                //print("st");
                
                //print("st2");
                placeVesselForDock(transactionVessel, placePort, vessel, part, missionOffering.DockingDistance);
                ScreenMessages.PostScreenMessage(missionOffering.VehicleName + " docked", 4, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                 ScreenMessages.PostScreenMessage(missionOffering.VehicleName + " rendezvoused", 4, ScreenMessageStyle.UPPER_CENTER);
            }
            missionUnderway = false;
            completeArrival = false;

            nextLogicTime = 0;
            ArrivalStage = -1;

            if (missionRepeat)
            {
                procureOffering(missionOffering, true);
            }
        }

        private ProtoVessel loadVessel(string folderName)
        {
            ConfigNode loadnode = null;
            if (!File.Exists(GamePath + folderName + "/vesselfile")) { abortArrival(); return null; }
            loadnode = ConfigNode.Load(GamePath + folderName + "/vesselfile");
            if (loadnode == null) { abortArrival(); return null; }
            ProtoVessel loadprotovessel = new ProtoVessel(loadnode, HighLogic.CurrentGame);
            return loadprotovessel;
        }

        private bool loadVesselForRendezvous(ProtoVessel placeVessel, Vessel targetVessel)
        {
            targetVessel.BackupVessel();

            placeVessel.orbitSnapShot = targetVessel.protoVessel.orbitSnapShot;

            placeVessel.orbitSnapShot.epoch = 0.0;

            tempID = rand.Next(1000000000).ToString();
            //rename any vessels present with "AdministativeDockingName"
            foreach (Vessel ve in FlightGlobals.Vessels)
            {
                if (ve.vesselName == tempID)
                {
                    Vessel NameVessel = null;
                    NameVessel = ve;
                    NameVessel.vesselName = "1";
                }
            }

            placeVessel.vesselID = Guid.NewGuid(); 

            placeVessel.vesselName = tempID;

            foreach (ProtoPartSnapshot p in placeVessel.protoPartSnapshots)
            {
                if (placeVessel.refTransform == p.flightID)
                {
                    p.flightID = (UInt32)rand.Next(1000000000, 2147483647);
                    placeVessel.refTransform = p.flightID;
                }
                else
                {
                    p.flightID = (UInt32)rand.Next(1000000000, 2147483647);
                }

                if (p.protoModuleCrew != null && p.protoModuleCrew.Count() != 0)
                {
                    List<ProtoCrewMember> cl = p.protoModuleCrew;
                    List<ProtoCrewMember> clc = new List<ProtoCrewMember>(cl);

                    foreach (ProtoCrewMember c in clc)
                    {
                        p.RemoveCrew(c);
                        //print("remove");
                    }
                }
            }

            try
            {
                placeVessel.Load(HighLogic.CurrentGame.flightState);
                return true;
            }
            catch
            {
                //abortArrival();
                //return false;
                return true;
            }
        }

        private void placeVesselForRendezvous(Vessel placeVessel, Vessel targetVessel)
        {
            Vector3d offset = new Vector3d();

            if (!determineRendezvousOffset(placeVessel, targetVessel, ref offset)) 
            {
                completeArrival = false;
                nextLogicTime = 0;
                ArrivalStage = -1;
                return;
            }

            placeVessel.orbit.UpdateFromStateVectors(targetVessel.orbit.pos + offset, targetVessel.orbit.vel, targetVessel.orbit.referenceBody, Planetarium.GetUniversalTime());
        }

        private bool determineRendezvousOffset(Vessel placeVessel, Vessel targetVessel, ref Vector3d offset)
        {
            int rendezvousDistance = vesselScale(placeVessel);

            //determine max scale of al vessels involved
            foreach (Vessel ves in FlightGlobals.Vessels)
            {
                if (!ves.packed && ves.loaded)
                {
                    //print("scale" + ves.vesselName);
                    int scale = vesselScale(ves);
                    //print("scale" + scale);
                    if (scale > rendezvousDistance)
                    {
                        rendezvousDistance = scale;
                    }
                }
            }

            bool good;
            int attempts = 0;

            //print("ren.dist" + rendezvousDistance);

            do
            {
                good = true;
                attempts = attempts + 1;
                //print("attempts:" + attempts);
                //print("rend " + rendezvousDistance);
                if (rendezvousDistance > 115) { rendezvousDistance = 115; }
                if (rendezvousDistance <= 0) { rendezvousDistance = 100; }

            
                ///make a random offset
                double x = 0;
                double y = 0;
                double z = 0;
                int ra = rand.Next(3);
                //print("ra " + ra);
                switch (ra)
                {
                    case 0:
                        x = (double)rendezvousDistance;
                        y = (double)rand.Next(rendezvousDistance);
                        z = (double)rand.Next(rendezvousDistance);
                        break;
                    case 1:
                        x = (double)rand.Next(rendezvousDistance);
                        y = (double)rendezvousDistance;
                        z = (double)rand.Next(rendezvousDistance);
                        break;
                    case 2:
                        x = (double)rand.Next(rendezvousDistance);
                        y = (double)rand.Next(rendezvousDistance);
                        z = (double)rendezvousDistance;
                        break;
                }
                //print("x" + x);
                //print("y" + y);
                //print("z" + z);
                if (rand.Next(0, 2) == 1)
                {
                    x = x * -1;
                }
                if (rand.Next(0, 2) == 1)
                {
                    y = y * -1;
                }
                if (rand.Next(0, 2) == 1)
                {
                    z = z * -1;
                }
                //print("x" + x);
                //print("y" + y);
                //print("z" + z);

                offset.x = x;
                offset.y = y;
                offset.z = z;

                //print(offset);

                // check if offset is far enough from all vessels in area
                foreach (Vessel ves in FlightGlobals.Vessels)
                {
                    if (!ves.packed && ves.loaded && ves.id != placeVessel.id )
                    {
                        
                        var dist = Vector3.Distance(ves.orbit.pos, targetVessel.orbit.pos + offset);
                        //print("name vessel " + ves.vesselName + " " + dist);
                        if (dist < rendezvousDistance)
                        {
                            good = false;
                        }
                    }
                }

            } while(!good && attempts < 100);

            return (good);
        }

        private bool checkDockingPortCompatibility(Part placePort, Part targetPort)
        {
            ModuleDockingNode placeDockingNode = placePort.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
            ModuleDockingNode targetDockingNode = targetPort.Modules.OfType<ModuleDockingNode>().FirstOrDefault();

            return (placeDockingNode.nodeType == targetDockingNode.nodeType);
        }

        private int vesselScale(Vessel ves)
        {
            float longestdistance = 0f;
            foreach (Part p in ves.parts)
            {
                if (Math.Abs(p.orgPos[0] - ves.rootPart.orgPos[0]) > longestdistance) { longestdistance = Math.Abs(p.orgPos[0] - ves.rootPart.orgPos[0]); }
                if (Math.Abs(p.orgPos[1] - ves.rootPart.orgPos[1]) > longestdistance) { longestdistance = Math.Abs(p.orgPos[1] - ves.rootPart.orgPos[1]); }
                if (Math.Abs(p.orgPos[2] - ves.rootPart.orgPos[2]) > longestdistance) { longestdistance = Math.Abs(p.orgPos[2] - ves.rootPart.orgPos[2]); }
            }
            return (((int)(longestdistance * 4 + 10)));
        }

        private void placeVesselForDock(Vessel placeVessel, Part placePort, Vessel targetVessel, Part targetPort, float distanceFactor)
        {
            //print("st3");
            ModuleDockingNode placeDockingNode = placePort.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
            //print("st4");
            ModuleDockingNode targetDockingNode = targetPort.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
            //print("st5");
            //print("0 " + placeDockingNode.controlTransform.position);


            //print("p9 " + targetDockingNode.controlTransform.rotation);
            //print("r9 " + placeVessel.vesselTransform.rotation);
            //rotate vessel
            //placeDockingNode.MakeReferenceTransform();
            QuaternionD placeVesselRotation = targetDockingNode.controlTransform.rotation * Quaternion.Euler(180f, (float)rand.Next(0, 360), 0);
            placeVessel.SetRotation(placeVesselRotation);
            //
            placeVessel.SetRotation(placeDockingNode.controlTransform.rotation * Quaternion.Euler(0, (float)rand.Next(0, 360), 0));
            //print("ae " + placeVessel.vesselTransform.rotation);
            //print("as " + placeVesselRotation);

            //print("aw " + Quaternion.FromToRotation(placeVessel.vesselTransform.up, -placeDockingNode.controlTransform.up).eulerAngles.ToString());
            //print("as " + placeDockRotation.eulerAngles.ToString());

            //Vector3.Cross

            //QuaternionD placeVesselRotation = placeDockRotation - (placeDockingNod.controlTransform.rotation -

            //print("aa" + placeDockingNode.nodeTransform.forward.normalized);
            //print("aa" + placeDockingNode.nodeTransform.forward.normalized);

            //print(Vector3.Angle(Vector3.Cross(placeDockingNode.nodeTransform.forward.normalized, placeVessel.vesselTransform.up.normalized),Vector3.Cross(placeVessel.vesselTransform.forward.normalized, placeVessel.vesselTransform.up.normalized)));
            //print(Vector3.Angle(Vector3.Cross(targetDockingNode.nodeTransform.forward.normalized, placeVessel.vesselTransform.up.normalized), Vector3.Cross(placeVessel.vesselTransform.forward.normalized, placeVessel.vesselTransform.up.normalized)));

            float anglePlaceDock = angleNormal(placeDockingNode.nodeTransform.forward.normalized, placeVessel.vesselTransform.forward.normalized, placeVessel.vesselTransform.up.normalized);
            float angleTargetDock = angleNormal(-targetDockingNode.nodeTransform.forward.normalized, placeVessel.vesselTransform.forward.normalized, placeVessel.vesselTransform.up.normalized);

            //print(anglePlaceDock - angleTargetDock);

            //print(anglePlaceDock);
            //print(angleTargetDock);

            placeVessel.SetRotation(placeVessel.vesselTransform.rotation * Quaternion.Euler(0, (anglePlaceDock - angleTargetDock), 0));

            //print("p1 " + placeVessel.ReferenceTransform.position);
            //print("p1 " + placeVessel.ReferenceTransform.rotation);
            //print("r1 " + placeVessel.vesselTransform.rotation);
            //position vessel

            /*print("1 " + targetDockingNode.controlTransform.position);
            print("1.3 " + targetDockingNode.controlTransform.up.normalized);*/
            //print((placeDockingNode.controlTransform.up.normalized - placeDockingNode.controlTransform.position));

            Vector3d placePortLocation = targetDockingNode.nodeTransform.position + (targetDockingNode.nodeTransform.forward.normalized * distanceFactor);
            //Vector3d placePortLocation = targetDockingNode.controlTransform.position + (targetDockingNode.controlTransform.up.normalized * distanceFactor);
            /*print("2 " + placePortLocation);
            print("3 " + (placePort.transform.position - placeVessel.ReferenceTransform.position));
            print("4 " + (placePort.transform.position - placeVessel.ReferenceTransform.position));*/

            //print("12 " + placeVessel.ReferenceTransform.position);
            Vector3d placeVesselPosition = placePortLocation + (placeVessel.vesselTransform.position - placeDockingNode.nodeTransform.position);
            //Vector3d placeVesselPosition = placePortLocation + (placeVessel.ReferenceTransform.position - placeDockingNode.nodeTransform.position);
            //Vector3d placeVesselPosition = placePortLocation + (placeVessel.ReferenceTransform.position - placeDockingNodecontrolTransform.position);
            //print("4 " + placeVesselPosition);
            //Vector3d FlightVesselPosition = new Vector3d((placeVesselPosition - targetVessel.mainBody.transform.position).x, (placeVesselPosition - targetVessel.mainBody.transform.position).z, (placeVesselPosition - targetVessel.mainBody.transform.position).y);
            //print((FlightGlobals.ActiveVessel.orbit.pos - FlightVesselPosition));

            placeVessel.SetPosition(placeVesselPosition);
            //print("q " + placeVessel.vesselTransform.position);
            //print("w " +placeVessel.ReferenceTransform.position);
            //print(placeVessel.vesselTransform.position);
        }


        //Thanks to NavyFish's Docking Port Alignment Indicator for showing how to calculate an angle 
        private float angleNormal(Vector3 measure, Vector3 reference, Vector3 axis)
        {
            //in contrast to NavyFish's Docking Port Alignment Indicator we need a 0-360 value
            return (angleSigned(Vector3.Cross(measure, axis), Vector3.Cross(reference, axis), axis) + 180);
        }

        //-180 to 180 angle
        private float angleSigned(Vector3 measureAngle, Vector3 referenceAngle, Vector3 axis)
        {
            if (Vector3.Dot(Vector3.Cross(measureAngle, referenceAngle), axis) < 0) //greater than 90 i.e v1 left of v2
                return -Vector3.Angle(measureAngle, referenceAngle);
            return Vector3.Angle(measureAngle, referenceAngle);
        }

        private void abortArrival()
        {
            missionUnderway = false;
            completeArrival = false;
            missionRepeat = false;

            nextLogicTime = 0;
            ArrivalStage = -1;
        }

        //Thanks to sarbian's Kerbal Crew Manifest for showing all this crew handling stuff
        private void handleLoadCrew(Vessel ves, int crewCount, int minCrew)
        {
            //print("crewCount " + crewCount);
            //print(ves.GetCrewCapacity());
            if (ves.GetCrewCapacity() < crewCount)
                crewCount = ves.GetCrewCapacity();

            string[] prefCrewNames = new string[0];
            getPreferredCrewNames(ref prefCrewNames);

            foreach (Part p in ves.parts)
            {
                if (p.CrewCapacity > p.protoModuleCrew.Count)
                {
                    //print(p.CrewCapacity + " " + p.protoModuleCrew.Count);
                    for (int i = 0; i < p.CrewCapacity && crewCount > 0; i++)
                    {
                        bool added = false;

                        //tourist
                        if (minCrew <= 0)
                        {
                            foreach (String name in prefCrewNames)
                            {
                                if (!added)
                                {
                                    foreach (ProtoCrewMember cr in HighLogic.CurrentGame.CrewRoster.Tourist)
                                    {
                                        if (name == cr.name && cr.rosterStatus == ProtoCrewMember.RosterStatus.Available)
                                        {
                                            if (AddCrew(p, cr))
                                            {
                                                crewCount = crewCount - 1;
                                                added = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //preferred crew
                        foreach (String name in prefCrewNames)
                        {
                            if (!added)
                            {
                                foreach (ProtoCrewMember cr in HighLogic.CurrentGame.CrewRoster.Crew)
                                {
                                    if (name == cr.name && cr.rosterStatus == ProtoCrewMember.RosterStatus.Available)
                                    {
                                        if (AddCrew(p, cr))
                                        {
                                            crewCount = crewCount - 1;
                                            minCrew = minCrew - 1;
                                            added = true;

                                        }
                                    }
                                }
                            }
                        }
                        //next crew or new crew
                        //print("one crew start");
                        //print("crew" + kerbal.name);
                        if (!added)
                        {
                            ProtoCrewMember crew = null;
                            crew = HighLogic.CurrentGame.CrewRoster.GetNextAvailableKerbal();
                            if (crew != null)
                            {
                                if (AddCrew(p, crew))
                                {
                                    crewCount = crewCount - 1;
                                    minCrew = minCrew - 1;
                                    added = true;
                                }
                            }
                        }
                    }
                }
            }
            ves.SpawnCrew();
        }

        private bool crewAvailable(Offering Off)
        {
            int availableCrew = 0;
            foreach (ProtoCrewMember cr in HighLogic.CurrentGame.CrewRoster.Crew)
            {
                if (cr.rosterStatus == ProtoCrewMember.RosterStatus.Available)
                {
                    availableCrew++;
                }
            }

            if (availableCrew < Off.MinimumCrew)
            {
                ScreenMessages.PostScreenMessage("not enough crew was available for mission", 4, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }

        private bool AddCrew(Part p, ProtoCrewMember kerbal)
        {
            p.AddCrewmember(kerbal);

            kerbal.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;

            if (kerbal.seat != null)
                kerbal.seat.SpawnCrew();

            return (true);
        }

        private void handleUnloadCrew(Vessel ves, bool savereturn)
        {
            foreach (Part p in ves.parts)
            {
                if (p.CrewCapacity > 0 && p.protoModuleCrew.Count > 0)
                {
                    for (int i = p.protoModuleCrew.Count - 1; i >= 0; i--)
                    {
                        unloadCrew(p.protoModuleCrew[i], p, savereturn);
                    }
                }
            }
            ves.DespawnCrew();
        }

        private void unloadCrew(ProtoCrewMember crew, Part p, bool savereturn)
        {
            //print("rem " + crew.name);
            p.RemoveCrewmember(crew);
            //crew.seat.DespawnCrew();
            //crew.seat = null;
            if (savereturn)
            {
                crew.rosterStatus = ProtoCrewMember.RosterStatus.Available;
            }
            else
            {
                if (HighLogic.CurrentGame.Parameters.Difficulty.MissingCrewsRespawn)
                {
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Missing;
                }
                else
                {
                    crew.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                }
            }
        }

        private bool staticorbit()
        {
            //Determine deviation between saved values and current values if they don't changed to much update the staticTime and return staticTime if not return zero.
            //the parameter AnomalyAtEpoch changes over time and must be excluded from analysis

            float MaxDeviationValue = 10;

            bool[] parameters = new bool[5];
            parameters[0] = false;
            parameters[1] = false;
            parameters[2] = false;

            //lenght
            if (MaxDeviationValue / 100 > Math.Abs(((vessel.orbit.semiMajorAxis - SMAsave) / SMAsave))) { parameters[0] = true; }
            //ratio
            if (MaxDeviationValue / 100 > Math.Abs(vessel.orbit.eccentricity - ECCsave)) { parameters[1] = true; }

            float angleD = MaxDeviationValue;
            //angle
            if (angleD > Math.Abs(vessel.orbit.inclination - INCsave) || angleD > Math.Abs(Math.Abs(vessel.orbit.inclination - INCsave) - 360)) { parameters[2] = true; }

            //print("SMA " + parameters[0] + ((vessel.orbit.semiMajorAxis - SMAsave) / SMAsave));
            //print("ECC " + parameters[1] + Math.Abs(vessel.orbit.eccentricity - ECCsave));
            //print("INC " + parameters[2] + Math.Abs(vessel.orbit.inclination - INCsave) + " or " + Math.Abs(Math.Abs(vessel.orbit.inclination - INCsave) - 360));


            if (parameters[0] == false || parameters[1] == false || parameters[2] == false)
            {
                return (false);
            }
            else
            {
                return (true);
            }
        }

        private bool checkDocked()
        {
            ModuleDockingNode dockingPort = part.Modules.OfType<ModuleDockingNode>().FirstOrDefault();

            //print(vessel.vesselName + " " + (dockingPort.state.Length >= 6 && dockingPort.state.Substring(0, 6) == "Docked" && null != dockingPort.vesselInfo.name));

            //this port is docked
            if (dockingPort.state.Length >= 6 && dockingPort.state.Substring(0, 6) == "Docked" && null != dockingPort.vesselInfo.name)
                return (true);

            //no joined port filled
            if (dockingPort.dockedPartUId == 0) {return (false);}

            //find joined port
            foreach (Part p in vessel.parts)
            {
                if (p.flightID == dockingPort.dockedPartUId)
                {
                    foreach (PartModule pm in p.Modules)
                    {
                        if (pm.ClassName == "ModuleDockingNode")
                        {
                            ModuleDockingNode joinedDockingPort = p.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
                            if (part.flightID == joinedDockingPort.dockedPartUId)
                            {
                                if (joinedDockingPort.state.Length >= 6 && joinedDockingPort.state.Substring(0, 6) == "Docked" && null != joinedDockingPort.vesselInfo.name)
                                    return (true);
                            }
                        }
                    }
                }
            }
            return (false);
        }

        private bool checkDocking()
        {
            ModuleDockingNode dockingPort = part.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
            //print(vessel.vesselName + " " + (dockingPort.state.Length >= 6 && dockingPort.state.Substring(0, 6) == "Docked" && null != dockingPort.vesselInfo.name));
            if (dockingPort.state.Length >= 7 && dockingPort.state.Substring(0, 7) == "Acquire")
                return (true);
            else
                return (false);
        }


        private void handleAutoDepart()
        {
            //print("handleAutoDepart");
            //print(offeringAllowed(commercialvehicleOffering));
            //print(returnResourcesAvailable(commercialvehicleOffering));
            //print(minimalCrewPresent(commercialvehicleOffering));
            //print(commercialvehiclePartCount + " " + countVesselParts(vessel));
            if (autoDepartAllowed(commercialvehicleOffering)
                && vesselClean(vessel)
                && returnResourcesAvailable(commercialvehicleOffering)
                && minimalCrewPresent(commercialvehicleOffering))
            {
                toMapView();
                handleContracts(vessel, false, true);

                if (commercialvehicleOffering.SafeReturn && HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                {
                    Funding.Instance.AddFunds(commercialvehicleOffering.VehicleReturnFee + cargoFee(), TransactionReasons.VesselRecovery);
                }

                handleUnloadCrew(vessel, commercialvehicleOffering.SafeReturn);
                vessel.Unload();
                vessel.Die();
                
                ScreenMessages.PostScreenMessage(commercialvehicleOffering.VehicleName + " returned to Kerbin", 4, ScreenMessageStyle.UPPER_CENTER);
            }
            else
            {
                //print("no depart");
                vehicleAutoDepart = false;
            }
        }

        private double cargoFee()
        {
            double fee = 0.0;
            
            if (commercialvehicleOffering.ReturnCargoMass == 0) { return 0; }

            double cargoMass = commercialvehicleOffering.ReturnCargoMass;
            string[] cargoArray = new string[0];
            getCargoArray(ref cargoArray);

            orderCargoArray(ref cargoArray);

            foreach (String s in cargoArray)
            {
                foreach (Part p in vessel.parts)
                {
                    foreach (PartResource r in p.Resources)
                    {
                        if (r.info.name == s)
                        {
                            //print(r.info.name);
                            if (r.amount != 0)
                            {
                                if (mass(r.info.name, r.amount) <= cargoMass)
                                {
                                    fee = fee + cost(r.info.name, r.amount);
                                    cargoMass = cargoMass - mass(r.info.name, r.amount);
                                }
                                else
                                {
                                    fee = fee + ((cargoMass / mass(r.info.name, r.amount)) * cost(r.info.name, r.amount));
                                    //print(fee);
                                    return fee;
                                }
                            }
                        }
                    }
                }
            }
            //print(fee);
            return fee;
        }


        private void orderCargoArray(ref string[] cargoArray)
        {
            string[] unorderCargoArray = new string[cargoArray.Length];
            double[] costPerMass = new double[cargoArray.Length];

            for (int i = 0; i < cargoArray.Length; i++)
            {
                unorderCargoArray[i] = cargoArray[i];
                PartResourceDefinition prd = PartResourceLibrary.Instance.GetDefinition(cargoArray[i]);
                costPerMass[i] = prd.unitCost / prd.density;
            }

            for (int u = 0; u < cargoArray.Length; u++)
            {
                int highestCargoResource = -1;
           
                for (int i = 0; i < cargoArray.Length; i++)
                {
                    if (unorderCargoArray[i] != "")
                    {
                        if (highestCargoResource != -1)
                        {
                            if (costPerMass[i] > costPerMass[highestCargoResource])
                                highestCargoResource = i;
                        }
                        else
                        {
                            highestCargoResource = i;
                        }
                    }
                }

                if (highestCargoResource != -1)
                {
                    cargoArray[u] = unorderCargoArray[highestCargoResource];
                    unorderCargoArray[highestCargoResource] = "";
                }
            }
        }


        private bool otherModulesCompletingArrival()
        {
            foreach (Part p in vessel.parts)
            {
                if (p.flightID != part.flightID)
                {
                    foreach (PartModule pm in p.Modules)
                    {
                        if (pm.ClassName == "RMMModule")
                        {
                            RMMModule otherComOffMod = p.Modules.OfType<RMMModule>().FirstOrDefault();
                            if (otherComOffMod.completeArrival) { return (true); }
                        }
                    }
                }
            }
            return (false);
        }

        private bool autoDepartAllowed(Offering Off)
        {
            if (offeringAllowed(Off))
            {
                return (true);
            }
            else
            {
                ScreenMessages.PostScreenMessage("not rated for this orbit", 4, ScreenMessageStyle.UPPER_CENTER);
                return (false);
            }
        }

        private bool vesselClean(Vessel ves)
        {
            if (commercialvehiclePartCount >= countVesselParts(ves))
            {
                return (true);
            }
            else
            {
                ScreenMessages.PostScreenMessage("vessel in unknown configuration for return", 4, ScreenMessageStyle.UPPER_CENTER);
                return (false);
            }

        }

        private bool minimalCrewPresent(Offering Off)
        {
            if (Off.MinimumCrew == 0) { return (true); }
            if (Off.MinimumCrew > astronautCrewCount(vessel)) { ScreenMessages.PostScreenMessage("not enough crew for return", 4, ScreenMessageStyle.UPPER_CENTER); return (false); }
            if (!Off.SafeReturn && crewCount(vessel) > 0) { ScreenMessages.PostScreenMessage("not rated for safe crew return", 4, ScreenMessageStyle.UPPER_CENTER); return (false); }
            return (true);
        }

        private bool returnResourcesAvailable(Offering Off)
        {
            if (Off.ReturnResources == "") { return (true); }

            string[] SplitArray = Off.ReturnResources.Split(',');

            string[] arrResource = new string[0];
            getProppellantArray(ref arrResource);

            foreach (String st in SplitArray)
            {
                string[] SplatArray = st.Split(':');
                string resourceName = SplatArray[0].Trim();
                double amount = Convert.ToDouble(SplatArray[1].Trim());

                if (!arrResource.Contains(resourceName)) { ScreenMessages.PostScreenMessage("vessel in unknown configuration for return", 4, ScreenMessageStyle.UPPER_CENTER); return (false); }

                if (amount * 0.99 > readResource(vessel, resourceName)) { ScreenMessages.PostScreenMessage("not enough resources for return", 4, ScreenMessageStyle.UPPER_CENTER); return (false); }
            }

            return (true);
        }

        private bool returnCargoMassNotExceeded(Offering Off)
        {
            if (Off.ReturnCargoMass == 0.0 || getCargoMass() <= Off.ReturnCargoMass * 1.1)
            {
                return true;
            }
            else
            {
                ScreenMessages.PostScreenMessage("cargo mass exceeds rated amount", 4, ScreenMessageStyle.UPPER_CENTER); 
                return false;
            }
        }


        private int countVesselParts(Vessel ves)
        {
            int count = 0;
            foreach (Part p in ves.parts)
            {
                count = count + 1;
            }
            return (count);
        }

        public void toMapView()
        {
            if (DevMode || MapView.MapIsEnabled) { return; }
            MapView.EnterMapView();
        }
        /// <summary>
        /// GUI Main
        /// </summary>
        /// <param name="windowID"></param>
        private void WindowGUIMain(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 200, 30));
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label("Current:", labelStyle, GUILayout.Width(200));
            if (GUILayout.Button(selectedOffering.Name, buttonStyle, GUILayout.Width(200), GUILayout.Height(22)))
            {
                GUIOffering = selectedOffering;
                intCrewCount = missionCrewCount;
                openGUIOffering();
            }
            if (missionUnderway)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Underway", labelStyle, GUILayout.Width(100));
                if (GUILayout.Button("Cancel", buttonStyle, GUILayout.Width(100), GUILayout.Height(22)))
                {
                    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                    {
                        if (Planetarium.GetUniversalTime() < missionArrivalTime - selectedOffering.Time + 3600)
                        {
                            if (Funding.Instance.Funds > GUIOffering.Price)
                            {
                                Funding.Instance.AddFunds(GUIOffering.Price, TransactionReasons.VesselRecovery);
                            }
                        }
                    }
                    missionUnderway = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("ETA:", labelStyle, GUILayout.Width(35));
                GUILayout.Label(timeETAString(missionArrivalTime - Planetarium.GetUniversalTime()), labelStyle, GUILayout.Width(165));
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("", labelStyle, GUILayout.Width(100));
            }
            GUILayout.EndVertical();

            GUILayout.Label("  ", labelStyle, GUILayout.Width(10));

            GUILayout.BeginVertical();
            missionRepeat = GUILayout.Toggle(missionRepeat, "Repeat");
            GUILayout.Label("Repeat Delay:", labelStyle, GUILayout.Width(80));
            GUILayout.BeginHorizontal();
                        
            if (GUILayout.Button("<", buttonStyle, GUILayout.Width(15), GUILayout.Height(15))) 
            {
                if (missionRepeatDelay >= 2) { missionRepeatDelay -= (missionRepeatDelay / 10 > 1 ? missionRepeatDelay/10 : 1); }
            }
            GUILayout.Label( missionRepeatDelay.ToString(), labelStyle, GUILayout.Width(25));
            if (GUILayout.Button(">", buttonStyle, GUILayout.Width(15), GUILayout.Height(15)))
            {
                if (missionRepeatDelay <= 999) { missionRepeatDelay += (missionRepeatDelay / 10 > 1 ? missionRepeatDelay / 10 : 1); }
                if (missionRepeatDelay > 999) { missionRepeatDelay = 999; }
            }
            GUILayout.Label("days", labelStyle, GUILayout.Width(30));
                        
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            scrollPositionAvailableCommercialOfferings = GUILayout.BeginScrollView(scrollPositionAvailableCommercialOfferings, false, true, GUILayout.Width(200), GUILayout.Height(300));
            foreach (Offering Off in OfferingsList)
            {
                if (GUILayout.Button(Off.Name, buttonStyle, GUILayout.Width(165), GUILayout.Height(22)))
                {
                    GUIOffering = Off;
                    if (GUIOffering.MinimumCrew > 0)
                    {
                        if (GUIOffering.MinimumCrew < GUIOffering.MaximumCrew)
                        {
                            intCrewCount = GUIOffering.MaximumCrew;
                        }
                        else
                        {
                            intCrewCount = GUIOffering.MinimumCrew;
                        }
                    }
                    else if (GUIOffering.MinimumCrew < GUIOffering.MaximumCrew)
                    {
                        intCrewCount = GUIOffering.MaximumCrew;
                    }
                    else if (GUIOffering.MinimumCrew == 0 && GUIOffering.MaximumCrew == 0)
                    {
                        intCrewCount = 0;
                    }

                    openGUIOffering();
                }
            }
            GUILayout.EndScrollView();

            //development mode buttons
            if (DevMode)
            {
                if (GUILayout.Button("Docking Stage 2"))
                {
                    dockStage2();
                }

                if (GUILayout.Button("Docking Stage 3"))
                {
                    dockStage3();
                }
                if (GUILayout.Button("Save vessel"))
                {
                    ConfigNode savenode = new ConfigNode();
                    vessel.BackupVessel();
                    vessel.protoVessel.Save(savenode);
                    savenode.Save(GamePath + CommercialOfferingsPath + "/Missions/DevMode/vesselfile");
                }
            }
            //

            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(200), GUILayout.Height(22)))
            {
                closeGUIMain();
            }
            GUILayout.EndVertical();
        }

        private void drawGUIMain()
        {
            windowPosGUIMain.xMin = windowGUIMainX;
            windowPosGUIMain.yMin = windowGUIMainY;
            windowPosGUIMain.width = windowGUIMainWidth;
            windowPosGUIMain.height = 450;
            windowPosGUIMain = GUILayout.Window(3404, windowPosGUIMain, WindowGUIMain, "Docking Port " + PortCode, windowStyle);
        }



//        [KSPEvent(name = "ordering", isDefault = false, guiActive = true, guiName = "Available Missions")]
        public void ordering()
        {
            openGUIMain();
        }

        public void openGUIMain()
        {
            closeGUIMain();
            initStyle();
            loadOfferings();

            if (selectedOffering != null)
            {
                
            }

            RenderingManager.AddToPostDrawQueue(340, new Callback(drawGUIMain));
        }

        public void closeGUIMain()
        {
            RenderingManager.RemoveFromPostDrawQueue(340, new Callback(drawGUIMain));
        }

        public void loadOfferings()
        {
            OfferingsList.Clear();


            //print("loading offerings");
            //load standard offerings
            //            string[] directoryOfferings = Directory.GetDirectories(GamePath + CommercialOfferingsPath + "/Offerings");
            //
            //            foreach (String dir in directoryOfferings)
            //            {
            //                //print(dir);
            //                Offering Off = new Offering();
            //
            //                Off.folderName = Path.GetFileName(dir);
            //                if (File.Exists(GamePath + CommercialOfferingsPath + "/Offerings/" + Off.folderName + "/info.txt"))
            //                {
            //                    Off.loadOffering(GamePath + CommercialOfferingsPath + "/Offerings/" + Off.folderName + "/info.txt");
            //
            //                    if (offeringAllowed(Off))
            //                    {
            //                        OfferingsList.Add(Off);
            //                        //print("loaded " + Off.Name);
            //                    }
            //                }
            //            }

            //print("aaa " + GamePath);

            loadOfferingsDirectory(GamePath + "/GameData");


            if (missionFolderName != "")
            {
                foreach (Offering Off in OfferingsList)
                {
                    if (Off.folderName == missionFolderName)
                    {
                        selectedOffering = Off;
                    }
                }
            }
        }

        private void loadOfferingsDirectory(string searchDirectory)
        {
            
            if (File.Exists(searchDirectory + "/CommercialOfferingsPackMarkerFile"))
            {
                string[] directoryOfferings = Directory.GetDirectories(searchDirectory);

                foreach (String dir in directoryOfferings)
                {
                    
                    Offering Off = new Offering();
                    
                    Off.folderName = dir.Substring(GamePath.ToString().Length, dir.Length - GamePath.ToString().Length);
                    
                    if (File.Exists(GamePath + Off.folderName + "/info.txt"))
                    {
                        Off.loadOffering(GamePath + Off.folderName + "/info.txt");

                        if (offeringAllowed(Off))
                        {
                            OfferingsList.Add(Off);
                            //print("loaded " + Off.Name);
                        }
                    }
                }
            }
            else
            {
                string[] searchDirectories = Directory.GetDirectories(searchDirectory);

                foreach (String dir in searchDirectories)
                {
                    loadOfferingsDirectory(dir);
                }
            }
        }

        private bool isCurrentCampaign(Offering off)
        {
            if (isACampaign(off))
            {
                if (off.CompanyName.Length >= 14 && off.CompanyName.Substring(14) == HighLogic.SaveFolder)
                {
                    return (true);
                }
            }
            else
            {
                return (true);
            }
            return (false);
        }


        private bool isACampaign(Offering off)
        {
            if (off.CompanyName.Length >= 14 && off.CompanyName.Substring(0, 14) == "KSPCAMPAIGN:::")
                return (true);
            return (false);
        }

        /// <summary>
        /// GUI Offering
        /// </summary>
        /// <param name="windowID"></param>
        private void WindowGUIOffering(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 500, 30));
            GUILayout.BeginVertical();

            if (!isACampaign(GUIOffering))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Company:", labelStyle, GUILayout.Width(100));
                GUILayout.Label(GUIOffering.CompanyName, labelStyle, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Vehicle:", labelStyle, GUILayout.Width(100));
            GUILayout.Label(GUIOffering.VehicleName, labelStyle, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Launch System:", labelStyle, GUILayout.Width(100));
            GUILayout.Label(GUIOffering.LaunchSystemName, labelStyle, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Price:", labelStyle, GUILayout.Width(100));
            GUILayout.Label(GUIOffering.Price.ToString() + ((GUIOffering.VehicleReturnFee > 0) ? " (" + (GUIOffering.Price - GUIOffering.VehicleReturnFee).ToString() + " with vehicle return)" : ""), labelStyle, GUILayout.Width(250));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Arrival in:", labelStyle, GUILayout.Width(100));
            GUILayout.Label(timeString(GUIOffering.Time), labelStyle, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            if (GUIOffering.MinimumCrew > 0)
            {
                if (GUIOffering.MinimumCrew < GUIOffering.MaximumCrew)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Minimal crew required:", labelStyle, GUILayout.Width(150));
                    GUILayout.Label(GUIOffering.MinimumCrew.ToString(), labelStyle, GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Maximum crew capacity:", labelStyle, GUILayout.Width(150));
                    GUILayout.Label(GUIOffering.MaximumCrew.ToString(), labelStyle, GUILayout.Width(200));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Crew:", labelStyle, GUILayout.Width(100));
                    GUILayout.Label(GUIOffering.MinimumCrew.ToString(), labelStyle, GUILayout.Width(200));
                    GUILayout.EndHorizontal();
                }
            }
            else if (GUIOffering.MinimumCrew < GUIOffering.MaximumCrew)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Crew capacity:", labelStyle, GUILayout.Width(100));
                GUILayout.Label(GUIOffering.MaximumCrew.ToString(), labelStyle, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }
            else if (GUIOffering.MinimumCrew == 0 && GUIOffering.MaximumCrew == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Unmanned", labelStyle, GUILayout.Width(100));
                GUILayout.EndHorizontal();
            }
            if (!GUIOffering.ReturnEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("No return mission", labelStyle, GUILayout.Width(300));
                GUILayout.EndHorizontal();
            }
            else if (!GUIOffering.SafeReturn)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("No safe return mission", labelStyle, GUILayout.Width(300));
                GUILayout.EndHorizontal();
            }
            if (GUIOffering.ReturnResources != "")
            {
                GUILayout.Label("Required return resources: ", labelStyle, GUILayout.Width(300));
                string[] SplitArray = GUIOffering.ReturnResources.Split(',');
                foreach (String st in SplitArray)
                {
                    string[] SplatArray = st.Split(':');
                    string resourceName = SplatArray[0].Trim();
                    double amount = Convert.ToDouble(SplatArray[1].Trim());
                    GUILayout.Label("   " + resourceName + ": " + RoundToSignificantDigits(amount,4).ToString(), labelStyle, GUILayout.Width(300));
                }
            }

            if (GUIOffering.ReturnCargoMass != 0.0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Return cargo mass: " + RoundToSignificantDigits(GUIOffering.ReturnCargoMass,3).ToString(), labelStyle, GUILayout.Width(300));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("", redlabelStyle, GUILayout.Width(430));
            if (GUILayout.Button("Delete", buttonStyle, GUILayout.Width(70), GUILayout.Height(22)))
            {
                if (Directory.Exists(GamePath + GUIOffering.folderName))
                {
                    DeleteDirectory(GamePath + GUIOffering.folderName);
                    closeGUIOffering();
                    loadOfferings();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (!missionUnderway)
            {
                if (GUILayout.Button("Procure", buttonStyle, GUILayout.Width(300), GUILayout.Height(22)))
                {
                    if (!missionUnderway)
                    {
                        GUIMission = GUIOffering;
                        openGUIMission();
                    }
                }
            }
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(200), GUILayout.Height(22)))
            {
                closeGUIOffering();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void drawGUIOffering()
        {
            windowPosGUIOffering = GUILayout.Window(3414, windowPosGUIOffering, WindowGUIOffering, GUIOffering.Name, windowStyle);
        }

        public void openGUIOffering()
        {
            closeGUIOffering();
            RenderingManager.AddToPostDrawQueue(341, new Callback(drawGUIOffering));
            print(GUIOffering.Name + " in folder: " + GUIOffering.folderName);
        }

        public void closeGUIOffering()
        {
            RenderingManager.RemoveFromPostDrawQueue(341, new Callback(drawGUIOffering));
        }

        /// <summary>
        /// GUI Order
        /// </summary>
        /// <param name="windowID"></param>
        private void WindowGUIMission(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 300, 30));
            GUILayout.BeginVertical();

            if (!isACampaign(GUIMission))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mission:", labelStyle, GUILayout.Width(100));
                GUILayout.Label(GUIMission.Name, labelStyle, GUILayout.Width(200));
                GUILayout.EndHorizontal();
            }

            if (GUIOffering.MinimumCrew < GUIOffering.MaximumCrew)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Planned crew:", labelStyle, GUILayout.Width(100));
                GUILayout.Label(intCrewCount.ToString(), labelStyle, GUILayout.Width(50));
                strCrewCount = GUILayout.TextField(strCrewCount, 3, GUILayout.Width(50));

                if (GUILayout.Button("set", buttonStyle, GUILayout.Width(50), GUILayout.Height(22)))
                {
                    //print(intCrewCount);
                    int.TryParse(strCrewCount, out intCrewCount);
                    //print(intCrewCount);
                    if (intCrewCount < GUIOffering.MinimumCrew) { intCrewCount = GUIOffering.MinimumCrew; }
                    if (intCrewCount > GUIOffering.MaximumCrew) { intCrewCount = GUIOffering.MaximumCrew; }
                    //print(intCrewCount);
                }
                GUILayout.EndHorizontal();
            }

            if (GUIOffering.MaximumCrew > 0)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("set preferred crew", buttonStyle, GUILayout.Width(300), GUILayout.Height(20)))
                {
                    openGUIPrefCrew();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("   ", labelStyle, GUILayout.Width(100));
            }

            if (GUILayout.Button("Confirm", buttonStyle, GUILayout.Width(300), GUILayout.Height(22)))
            {
                procureOffering(GUIOffering,false);
            }
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(300), GUILayout.Height(22)))
            {
                closeGUIPrefCrew();
                closeGUIMission();
            }

            GUILayout.EndVertical();
        }

        private void drawGUIMission()
        {
            windowPosGUIMission = GUILayout.Window(3444, windowPosGUIMission, WindowGUIMission, "Mission", windowStyle);
        }

        public void openGUIMission()
        {
            closeGUIMission();
            RenderingManager.AddToPostDrawQueue(344, new Callback(drawGUIMission));
        }

        public void closeGUIMission()
        {
            RenderingManager.RemoveFromPostDrawQueue(344, new Callback(drawGUIMission));
        }

        public void procureOffering(Offering Off,bool repeat)
        {
            if (missionUnderway == true) { ScreenMessages.PostScreenMessage("already a mission underway", 4, ScreenMessageStyle.UPPER_CENTER); return; }
            if (!offeringAllowed(Off)) { ScreenMessages.PostScreenMessage("not rated for this orbit", 4, ScreenMessageStyle.UPPER_CENTER); return; }

            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                if (Funding.Instance.Funds > Off.Price)
                {
                    Funding.Instance.AddFunds(-Off.Price, TransactionReasons.VesselRollout);
                }
                else
                {
                    ScreenMessages.PostScreenMessage("insufficient funds", 4, ScreenMessageStyle.UPPER_CENTER);
                    return;
                }
            }


            missionUnderway = true;
            missionFolderName = Off.folderName;
            if (!repeat)
            {
                missionArrivalTime = (float)(Planetarium.GetUniversalTime() + Off.Time);
                missionCrewCount = intCrewCount;
            }
            else
            {
                missionArrivalTime = (float)(Planetarium.GetUniversalTime() + (Off.Time > (missionRepeatDelay * 21600) ? Off.Time : (missionRepeatDelay * 21600)));
            }

            SMAsave = (float)vessel.orbit.semiMajorAxis;
            ECCsave = (float)vessel.orbit.eccentricity;
            INCsave = (float)vessel.orbit.inclination;

            selectedOffering = Off;
            closeGUIMission();
            closeGUIOffering();
        }


        /// <summary>
        /// GUI PrefCrew
        /// </summary>
        /// <param name="windowID"></param>
        private void WindowGUIPrefCrew(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 200, 30));
            GUILayout.BeginVertical();


            GUILayout.Label("Preferred:", labelStyle, GUILayout.Width(100));

            scrollPositionPreferredCrew = GUILayout.BeginScrollView(scrollPositionPreferredCrew, false, true, GUILayout.Width(200), GUILayout.Height(200));
            foreach (ProtoCrewMember cr in preferredCrewList)
            {
                if (GUILayout.Button(cr.type == ProtoCrewMember.KerbalType.Tourist ? cr.name + " T" : cr.name, buttonStyle, GUILayout.Width(165), GUILayout.Height(22)))
                {
                    preferredCrewList.Remove(cr);
                }
            }
            GUILayout.EndScrollView();
                
            GUILayout.Label("Roster:", labelStyle, GUILayout.Width(100));


            scrollPositionAvailableCrew = GUILayout.BeginScrollView(scrollPositionAvailableCrew, false, true, GUILayout.Width(200), GUILayout.Height(300));
            foreach (ProtoCrewMember cr in HighLogic.CurrentGame.CrewRoster.Crew)
            {
                if (GUILayout.Button(cr.name, buttonStyle, GUILayout.Width(165), GUILayout.Height(22)))
                {
                    bool alreadyAdded = false;
                    foreach (ProtoCrewMember cre in preferredCrewList)
                    {
                        if (cre.name == cr.name)
                        {
                            alreadyAdded = true;
                        }
                    }
                    if (!alreadyAdded) { preferredCrewList.Add(cr); }
                }
            }
            foreach (ProtoCrewMember to in HighLogic.CurrentGame.CrewRoster.Tourist)
            {
                if (GUILayout.Button(to.name + " T", buttonStyle, GUILayout.Width(165), GUILayout.Height(22)))
                {
                    bool alreadyAdded = false;
                    foreach (ProtoCrewMember cre in preferredCrewList)
                    {
                        if (cre.name == to.name)
                        {
                            alreadyAdded = true;
                        }
                    }
                    if (!alreadyAdded) { preferredCrewList.Add(to); }
                }
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set", buttonStyle, GUILayout.Width(100), GUILayout.Height(22)))
            {
                missionPreferedCrew = "";
                foreach (ProtoCrewMember cr in preferredCrewList)
                {
                    missionPreferedCrew = missionPreferedCrew + cr.name + ",";
                }
                closeGUIPrefCrew();
            }
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(100), GUILayout.Height(22)))
            {
                closeGUIPrefCrew();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void drawGUIPrefCrew()
        {
            windowPosGUIPrefCrew = GUILayout.Window(3447, windowPosGUIPrefCrew, WindowGUIPrefCrew, "Crew", windowStyle);
        }

        public void openGUIPrefCrew()
        {
            closeGUIPrefCrew();

            preferredCrewList.Clear();
            string[] prefCrewNames = new string[0];
            getPreferredCrewNames(ref prefCrewNames);
            foreach (String name in prefCrewNames)
            {
                foreach (ProtoCrewMember cr in HighLogic.CurrentGame.CrewRoster.Crew)
                {
                    if (name == cr.name) { preferredCrewList.Add(cr); }
                }
                foreach (ProtoCrewMember to in HighLogic.CurrentGame.CrewRoster.Tourist)
                {
                    if (name == to.name) { preferredCrewList.Add(to); }
                }
            }
            RenderingManager.AddToPostDrawQueue(347, new Callback(drawGUIPrefCrew));
        }

        public void closeGUIPrefCrew()
        {
            RenderingManager.RemoveFromPostDrawQueue(347, new Callback(drawGUIPrefCrew));
        }


        private void getPreferredCrewNames(ref string[] names)
        {
            string[] SplitArray = missionPreferedCrew.Split(',');

            Array.Resize(ref names, SplitArray.Length);
            Array.Copy(SplitArray, names, SplitArray.Length);
        }

        private double RoundToSignificantDigits(double d, int digits)
        {
            if (d == 0)
                return 0;

            double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1);
            return scale * Math.Round(d / scale, digits);
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        /// <summary>
        /// depart
        /// </summary>
        [KSPEvent(name = "setAutoDepart", isDefault = false, guiActive = false, guiName = "Commence Return")]
        public void setAutoDepart()
        {
            if (checkDocked())
            {
                ModuleDockingNode DockNode = part.Modules.OfType<ModuleDockingNode>().FirstOrDefault();
                DockNode.Undock();
            }
            vehicleAutoDepart = true;
            nextLogicTime = Planetarium.GetUniversalTime() + 2;
        }

        /// <summary>
        /// general functions
        /// </summary>
        /// <returns></returns>
        /// 
        private void initStyle()
        {
            windowStyle = new GUIStyle(HighLogic.Skin.window);
            windowStyle.stretchWidth = false;
            windowStyle.stretchHeight = false;

            labelStyle = new GUIStyle(HighLogic.Skin.label);
            labelStyle.stretchWidth = false;
            labelStyle.stretchHeight = false;

            redlabelStyle = new GUIStyle(HighLogic.Skin.label);
            redlabelStyle.stretchWidth = false;
            redlabelStyle.stretchHeight = false;
            redlabelStyle.normal.textColor = Color.red;

            textFieldStyle = new GUIStyle(HighLogic.Skin.textField);
            textFieldStyle.stretchWidth = false;
            textFieldStyle.stretchHeight = false;

            buttonStyle = new GUIStyle(HighLogic.Skin.button);
            buttonStyle.stretchHeight = false;
            buttonStyle.stretchWidth = false;
        }

        private bool offeringAllowed(Offering Off)
        {
            if (vessel.mainBody.name == Off.Body && (vesselOrbitAltitude() < Off.MaxOrbitAltitude || Off.MaxOrbitAltitude == 0) && isCurrentCampaign(Off))
            {
                return (true);
            }
            else
            {
                return (false);
            }
        }

        public double vesselOrbitAltitude()
        {
            return ((vessel.orbit.semiMajorAxis - vessel.mainBody.Radius) / 1000);
        }

        //return a day-hour-minute-seconds-time format for the time
        public string timeString(double time)
        {
            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            string strT = "";

            bool positive;

            if (time >= 0)
            {
                positive = true;
            }
            else
            {
                positive = false;
            }

            days = (int)Math.Floor(time / 21600);
            time = time - (days * 21600);

            hours = (int)Math.Floor(time / 3600);
            time = time - (hours * 3600);

            minutes = (int)Math.Floor(time / 60);
            time = time - (minutes * 60);

            seconds = (int)Math.Floor(time);

            if (days > 0)
            {
                strT = days.ToString() + "d";
                strT = (hours != 0) ? strT + hours.ToString() + "h" : strT;
                strT = (minutes != 0) ? strT + minutes.ToString() + "m" : strT;
                strT = (seconds != 0) ? strT + seconds.ToString() + "s" : strT;
            }
            else if (hours > 0)
            {
                strT = hours.ToString() + "h";
                strT = (minutes != 0) ? strT + minutes.ToString() + "m" : strT;
                strT = (seconds != 0) ? strT + seconds.ToString() + "s" : strT;
            }
            else if (minutes > 0)
            {
                strT = minutes.ToString() + "m";
                strT = (seconds != 0) ? strT + seconds.ToString() + "s" : strT;
            }
            else if (seconds > 0)
            {
                strT = seconds.ToString() + "s";
            }

            //strT = days.ToString() + "d" + hours.ToString() + "h" + minutes.ToString() + "m" + seconds + "s";

            if (positive)
                return (strT);
            else
                return ("-" + strT);
        }

        public string timeETAString(double time)
        {
            int days = 0;
            int hours = 0;
            int minutes = 0;
            int seconds = 0;

            string strT = "";

            if (time >= 0)
            {
                days = (int)Math.Floor(time / 21600);
                time = time - (days * 21600);

                hours = (int)Math.Floor(time / 3600);
                time = time - (hours * 3600);

                minutes = (int)Math.Floor(time / 60);
                time = time - (minutes * 60);

                seconds = (int)Math.Floor(time);

                if (days > 1)
                {
                    strT = days.ToString() + "d";
                }
                else if (days > 0)
                {

                    strT = days.ToString() + "d" + hours.ToString() + "h";
                }
                else if (hours > 0)
                {
                    strT = hours.ToString() + "h";
                }
                else
                {
                    strT = "soon(TM)";
                }
            }
            else
            {
                if (time > -3600)
                {
                    strT = "anytime now";
                }
                else
                {
                    strT = "maybe later";
                }
            }

            return (strT);
        }

        private void handleContracts(Vessel ves, bool arrive, bool depart)
        {
            Contract[] activeContracts = Contracts.ContractSystem.Instance.GetCurrentActiveContracts<FinePrint.Contracts.TourismContract>();
            foreach (Contract con in activeContracts)
            {

                if (ReferenceEquals(con.GetType(), typeof(FinePrint.Contracts.TourismContract)))
                {
                    for (int i = 0; i < con.ParameterCount; i++)
                    {
                        ContractParameter conpara1 = con.GetParameter(i);
                        if (ReferenceEquals(conpara1.GetType(), typeof(FinePrint.Contracts.Parameters.KerbalTourParameter)))
                        {
                            FinePrint.Contracts.Parameters.KerbalTourParameter ktp = (FinePrint.Contracts.Parameters.KerbalTourParameter)conpara1;

                            foreach (Part p in ves.parts)
                            {
                                if (p.CrewCapacity > 0 && p.protoModuleCrew.Count > 0)
                                {
                                    for (int c = 0; c < p.protoModuleCrew.Count; c++)
                                    {
                                        if (p.protoModuleCrew[c].name == ktp.kerbalName)
                                        {
                                            // complete destinations parameters on arrive for kerbals on vessel
                                            if (arrive)
                                            {
                                                for (int u = 0; u < conpara1.ParameterCount; u++)
                                                {
                                                    ContractParameter conpara2 = conpara1.GetParameter(u);
                                                    if (ReferenceEquals(conpara2.GetType(), typeof(FinePrint.Contracts.Parameters.KerbalDestinationParameter)))
                                                    {
                                                        FinePrint.Contracts.Parameters.KerbalDestinationParameter kds = (FinePrint.Contracts.Parameters.KerbalDestinationParameter)conpara2;

                                                        if (ves.mainBody.name == "Kerbin" || ves.mainBody.name == "Mun" || ves.mainBody.name == "Minmus")
                                                        {
                                                            if (kds.targetBody.name == "Kerbin" && (kds.targetType == FlightLog.EntryType.Orbit || kds.targetType == FlightLog.EntryType.Suborbit))
                                                            {
                                                                CompleteContractParameter(kds);
                                                            }
                                                        }
                                                        if (ves.mainBody.name == "Mun" || ves.mainBody.name == "Minmus")
                                                        {
                                                            if (kds.targetBody.name == ves.mainBody.name && (kds.targetType == FlightLog.EntryType.Orbit || kds.targetType == FlightLog.EntryType.Flyby))
                                                            {
                                                                CompleteContractParameter(kds);
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            // complete kerbaltour parameters on depart which have all their destinations completed
                                            if (depart)
                                            {
                                                bool allDestinationsSucceeded = true;
                                                for (int u = 0; u < conpara1.ParameterCount; u++)
                                                {
                                                    ContractParameter conpara2 = conpara1.GetParameter(i);
                                                    if (conpara2.State != Contracts.ParameterState.Complete)
                                                    {
                                                        allDestinationsSucceeded = false;
                                                    }
                                                }
                                                if (depart && allDestinationsSucceeded)
                                                {
                                                    CompleteContractParameter(ktp);
                                                    HighLogic.CurrentGame.CrewRoster.Remove(p.protoModuleCrew[c]);

                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Register Port Code logic
        /// </summary>
        [KSPEvent(name = "register", isDefault = false, guiActive = true, guiName = "Register Docking Port")]
        public void register()
        {
            initStyle();
            RenderingManager.AddToPostDrawQueue(4116, OnDrawRegister);
        }

        private void OnDrawRegister()
        {
            windowPosRegister = GUI.Window(157, windowPosRegister, WindowRegister, "Register", windowStyle);
        }

        private void WindowRegister(int windowID)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Docking Port Code");
            StrPortCode = GUILayout.TextField(StrPortCode, 15, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Register", GUILayout.Width(60)))
            {
                if (StrPortCode != "")
                {
                    PortCode = StrPortCode;
                    closeWindowRegister();
                }
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                closeWindowRegister();
            }
            GUILayout.EndHorizontal();
        }

        public void closeWindowRegister()
        {
            RenderingManager.RemoveFromPostDrawQueue(4116, OnDrawRegister);
        }
    }
}