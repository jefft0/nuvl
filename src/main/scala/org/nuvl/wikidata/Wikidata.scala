/*
Copyright (C) 2017 Jeff Thompson

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 */

package org.nuvl.wikidata

import scala.collection.mutable

object Wikidata {
  // Class Items
  final val QEntity = 35120;
  final val Qevent = 1190554;
  final val QIanaTimeZone = 17272692;
  final val Qtravel = 61509;

  // Individual Items
  final val QNull = 543287;

  // Properties
  final val PappliesToPart = 518;
  final val Parchitect = 84;
  final val Pas = 794;
  final val PcastMember = 161;
  final val PcontainsAdministrativeTerritorialEntity = 150;
  final val PcontainsSettlement = 1383;
  final val PcoordinateLocation = 625;
  final val Pcountry = 17;
  final val PdescribedAtUrl = 973;
  final val PdestinationPoint = 1444;
  final val Pdirection = 560;
  final val PdiscontinuedDate = 2669;
  final val PdissolvedOrAbolished = 576;
  final val PearliestDate = 1319;
  final val PendCause = 1534;
  final val PendPeriod = 3416;
  final val PendTime = 582;
  final val PequivalentClass = 1709;
  final val PexceptionToConstraint = 2303;
  final val Pexcluding = 1011;
  final val Pfollows = 155;
  final val PhasCause = 828;
  final val PiataAirportCode = 238;
  final val Pinception = 571;
  final val Pincluding = 1012;
  final val PinstanceOf = 31;
  final val PlatestDate = 1326;
  final val PlocatedInTheAdministrativeTerritorialEntity = 131;
  final val PlocatedInTimeZone = 421;
  final val PlocatedOnStreet = 669;
  final val PlocatedOnTerrainFeature = 706;
  final val Plocation = 276;
  final val PmainRegulatoryText = 92;
  final val Pof = 642;
  final val Poperator = 137;
  final val Pparticipant = 710;
  final val PpartOf = 361;
  final val PpointInTime = 585;
  final val Pproportion = 1107;
  final val PreasonForDeprecation = 2241;
  final val PreferenceUrl = 854;
  final val Preplaces = 1365;
  final val Pretrieved = 813;
  final val PserviceEntry = 729;
  final val PsignificantEvent = 793;
  final val PsourcingCircumstances = 1480;
  final val PstartPeriod = 3415;
  final val PstartPoint = 1427;
  final val PstartTime = 580;
  final val PstatedIn = 248;
  final val PstatementDisputedBy = 1310;
  final val PstreetNumber = 670;
  final val PsubclassOf = 279;
  final val PsubjectOf = 805;
  final val Puse = 366;
  final val PvalidInPeriod = 1264;
}
