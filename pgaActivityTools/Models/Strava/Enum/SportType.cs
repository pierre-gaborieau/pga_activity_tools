using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace pgaActivityTools.Models.Strava.Enum;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SportType
{
    [EnumMember(Value = "AlpineSki")]
    GravelRide, //Gravel
    [EnumMember(Value = "Run")]
    Run, //Running
    [EnumMember(Value = "Hike")]
    Hike, // Randonnée
    [EnumMember(Value = "MountainBikeRide")]
    MountainBikeRide, // VTT
    [EnumMember(Value = "TrailRun")]    
    TrailRun, // Course en sentier
    [EnumMember(Value = "VirtualRun")]
    VirtualRun, // Course virtuelle
    [EnumMember(Value = "Walk")]
    Walk, // Marche
    [EnumMember(Value = "Ride")]
    Ride, // Vélo
    [EnumMember(Value = "VirtualRide")]
    VirtualRide // Hometrainer
}
