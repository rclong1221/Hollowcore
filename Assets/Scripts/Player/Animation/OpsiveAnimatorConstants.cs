using UnityEngine;

namespace Player.Animation
{
    /// <summary>
    /// Constants for Opsive's animation system parameters.
    /// These match the values expected by ClimbingDemo.controller and other Opsive controllers.
    /// </summary>
    public static class OpsiveAnimatorConstants
    {
        #region Ability Indices
        // VERIFIED from Opsive source code [DefaultAbilityIndex] attributes
        
        /// <summary>No ability active - normal locomotion.</summary>
        public const int ABILITY_NONE = 0;
        
        /// <summary>Jump ability. From Jump.cs:25 [DefaultAbilityIndex(1)]</summary>
        public const int ABILITY_JUMP = 1;
        
        /// <summary>Fall ability. From Fall.cs:19 [DefaultAbilityIndex(2)]</summary>
        public const int ABILITY_FALL = 2;
        
        /// <summary>HeightChange ability (crouch). From HeightChange.cs:21 [DefaultAbilityIndex(3)]</summary>
        public const int ABILITY_HEIGHT_CHANGE = 3;
        
        #region Agility Pack Abilities (101-107)
        /// <summary>Dodge ability - quick directional dodge. From Dodge.cs [DefaultAbilityIndex(101)]</summary>
        public const int ABILITY_DODGE = 101;

        /// <summary>Roll ability - rolling evasion. From Roll.cs [DefaultAbilityIndex(102)]</summary>
        public const int ABILITY_ROLL = 102;

        /// <summary>Crawl ability - low crawling. From Crawl.cs [DefaultAbilityIndex(103)]</summary>
        public const int ABILITY_CRAWL = 103;

        /// <summary>Hang ability (Agility Pack). From Hang.cs [DefaultAbilityIndex(104)]</summary>
        public const int ABILITY_HANG = 104;

        /// <summary>Vault ability - climbing over obstacles. From Vault.cs [DefaultAbilityIndex(105)]</summary>
        public const int ABILITY_VAULT = 105;

        /// <summary>Ledge Strafe ability - narrow ledge movement. From LedgeStrafe.cs [DefaultAbilityIndex(106)]</summary>
        public const int ABILITY_LEDGE_STRAFE = 106;

        /// <summary>Balance ability - beam/narrow platform walking. From Balance.cs [DefaultAbilityIndex(107)]</summary>
        public const int ABILITY_BALANCE = 107;
        #endregion

        #region Swimming Pack Abilities (301-304)
        /// <summary>Swim ability (Swimming Pack). From Swim.cs [DefaultAbilityIndex(301)]</summary>
        public const int ABILITY_SWIM = 301;

        /// <summary>Dive ability - platform dive into water. From Dive.cs [DefaultAbilityIndex(302)]</summary>
        public const int ABILITY_DIVE = 302;

        /// <summary>Climb from water ability. From ClimbFromWater.cs [DefaultAbilityIndex(303)]</summary>
        public const int ABILITY_CLIMB_FROM_WATER = 303;

        /// <summary>Drown ability. From Drown.cs [DefaultAbilityIndex(304)]</summary>
        public const int ABILITY_DROWN = 304;
        #endregion

        #region Climbing Pack Abilities (501-503)
        /// <summary>Short climb ability (vault over obstacles).</summary>
        public const int ABILITY_SHORT_CLIMB = 501;
        
        /// <summary>Ladder climb ability.</summary>
        public const int ABILITY_LADDER_CLIMB = 502;
        
        /// <summary>Free climb ability (wall climbing).</summary>
        public const int ABILITY_FREE_CLIMB = 503;
        #endregion

        #region Ride Ability (12)
        /// <summary>Ride ability - mounting/riding/dismounting. From Ride.cs [DefaultAbilityIndex(12)]</summary>
        public const int ABILITY_RIDE = 12;
        #endregion

        #endregion // End Ability Indices
        
        #region Jump AbilityIntData Values
        // From Jump.cs:114: (m_AirborneJumpCount > 0 ? 2 : (m_Jumping ? 0 : 1))
        
        /// <summary>Currently jumping (in air, ascending).</summary>
        public const int JUMP_INT_JUMPING = 0;
        
        /// <summary>Landed / not jumping.</summary>
        public const int JUMP_INT_LANDED = 1;
        
        /// <summary>Airborne jump (double jump).</summary>
        public const int JUMP_INT_AIRBORNE = 2;
        #endregion
        
        #region Fall AbilityIntData Values
        // From Fall.cs
        
        /// <summary>Currently falling (in air).</summary>
        public const int FALL_INT_FALLING = 0;
        
        /// <summary>Just landed.</summary>
        public const int FALL_INT_LANDED = 1;
        #endregion
        
        #region Crouch AbilityIntData Values
        // From HeightChange.cs:22 [DefaultAbilityIntData(1)]
        
        /// <summary>Default crouch IntData.</summary>
        public const int CROUCH_INT_DATA = 1;
        #endregion

        #region Weapon Item IDs
        /// <summary>Trident (Swimming Melee) - from SwimmingFirstPersonArmsDemo.</summary>
        public const int ITEM_TRIDENT = 20;

        /// <summary>Underwater Gun (Swimming Ranged) - from SwimmingFirstPersonArmsDemo.</summary>
        public const int ITEM_UNDERWATER_GUN = 21;
        #endregion

        #region Sprint Start Directional IDs
        // AbilityIntData values for directional sprint starts (from controller analysis)
        public const int START_RUN_FWD = 9;
        public const int START_RUN_FWD_LEFT = 10;
        public const int START_RUN_FWD_RIGHT = 11;
        public const int START_RUN_LEFT = 12;
        public const int START_RUN_RIGHT = 13;
        public const int START_RUN_BACK = 14;
        public const int START_RUN_BACK_LEFT = 15;
        public const int START_RUN_BACK_RIGHT = 16;
        #endregion
        
        #region Dodge AbilityIntData Values
        // From Dodge.cs - directional dodge variants
        /// <summary>Dodge to the left.</summary>
        public const int DODGE_LEFT = 0;

        /// <summary>Dodge to the right.</summary>
        public const int DODGE_RIGHT = 1;

        /// <summary>Dodge forward.</summary>
        public const int DODGE_FORWARD = 2;

        /// <summary>Dodge backward.</summary>
        public const int DODGE_BACKWARD = 3;
        #endregion

        #region Roll AbilityIntData Values
        // From Roll.cs - directional roll variants
        /// <summary>Roll to the left.</summary>
        public const int ROLL_LEFT = 0;

        /// <summary>Roll to the right.</summary>
        public const int ROLL_RIGHT = 1;

        /// <summary>Roll forward.</summary>
        public const int ROLL_FORWARD = 2;

        /// <summary>Roll on landing (from fall).</summary>
        public const int ROLL_LAND = 3;
        #endregion

        #region Crawl AbilityIntData Values
        // From Crawl.cs
        /// <summary>Currently crawling.</summary>
        public const int CRAWL_ACTIVE = 0;

        /// <summary>Stopping crawl (getting up).</summary>
        public const int CRAWL_STOPPING = 1;
        #endregion

        #region Swim AbilityIntData Values
        // From Swim.cs:106-112 SwimStates enum
        /// <summary>Character entered water from air (fell/jumped in).</summary>
        public const int SWIM_ENTER_FROM_AIR = 0;

        /// <summary>Character is swimming on the surface.</summary>
        public const int SWIM_SURFACE = 1;

        /// <summary>Character is swimming underwater.</summary>
        public const int SWIM_UNDERWATER = 2;

        /// <summary>Character is exiting water while moving.</summary>
        public const int SWIM_EXIT_MOVING = 3;

        /// <summary>Character is exiting water while idle.</summary>
        public const int SWIM_EXIT_IDLE = 4;
        #endregion

        #region Dive AbilityIntData Values
        // From Dive.cs:56-60 DiveStates enum
        /// <summary>Shallow dive from low height.</summary>
        public const int DIVE_SHALLOW = 0;

        /// <summary>High dive from elevated platform.</summary>
        public const int DIVE_HIGH = 1;

        /// <summary>About to enter water.</summary>
        public const int DIVE_ENTER_WATER = 2;
        #endregion

        #region ClimbFromWater AbilityIntData Values
        // From ClimbFromWater.cs:54
        /// <summary>Not yet in position to climb.</summary>
        public const int CLIMB_WATER_NOT_IN_POSITION = 0;

        /// <summary>Climbing out while idle.</summary>
        public const int CLIMB_WATER_IDLE = 1;

        /// <summary>Climbing out while moving.</summary>
        public const int CLIMB_WATER_MOVING = 2;
        #endregion

        #region FreeClimb AbilityIntData Values
        /// <summary>Character is mounting from the bottom.</summary>
        public const int CLIMB_BOTTOM_MOUNT = 0;
        
        /// <summary>Character is mounting from the top.</summary>
        public const int CLIMB_TOP_MOUNT = 1;
        
        /// <summary>Character is actively climbing.</summary>
        public const int CLIMB_CLIMBING = 2;
        
        /// <summary>Character is turning a 90 degree corner that turns into the character.</summary>
        public const int CLIMB_INNER_CORNER = 3;
        
        /// <summary>Character is turning a 90 degree corner that turns away from the character.</summary>
        public const int CLIMB_OUTER_CORNER = 4;
        
        /// <summary>Character is dismounting from the bottom (dropping off).</summary>
        public const int CLIMB_BOTTOM_DISMOUNT = 5;
        
        /// <summary>Character is dismounting from the top (vaulting over).</summary>
        public const int CLIMB_TOP_DISMOUNT = 6;
        
        /// <summary>Character is starting to climb from horizontal hang.</summary>
        public const int CLIMB_HORIZONTAL_HANG_START = 7;
        
        /// <summary>Character is starting to climb from vertical hang.</summary>
        public const int CLIMB_VERTICAL_HANG_START = 8;
        #endregion

        #region Ride AbilityIntData Values
        // From Ride.cs - corresponds to RideState phases
        /// <summary>Mounting from left side.</summary>
        public const int RIDE_MOUNT_LEFT = 1;

        /// <summary>Mounting from right side.</summary>
        public const int RIDE_MOUNT_RIGHT = 2;

        /// <summary>Actively riding (seated on mount).</summary>
        public const int RIDE_RIDING = 3;

        /// <summary>Dismounting to left side.</summary>
        public const int RIDE_DISMOUNT_LEFT = 4;

        /// <summary>Dismounting to right side.</summary>
        public const int RIDE_DISMOUNT_RIGHT = 5;

        /// <summary>Dismount animation complete.</summary>
        public const int RIDE_COMPLETE = 6;
        #endregion
        
        #region Parameter Names
        /// <summary>Int parameter: currently active ability index.</summary>
        public const string PARAM_ABILITY_INDEX = "AbilityIndex";
        
        /// <summary>Bool parameter: triggers on ability change.</summary>
        public const string PARAM_ABILITY_CHANGE = "AbilityChange";
        
        /// <summary>Int parameter: sub-state within ability.</summary>
        public const string PARAM_ABILITY_INT_DATA = "AbilityIntData";
        
        /// <summary>Float parameter: blend direction within ability.</summary>
        public const string PARAM_ABILITY_FLOAT_DATA = "AbilityFloatData";
        
        /// <summary>Float parameter: horizontal movement input (-1 to 1).</summary>
        public const string PARAM_HORIZONTAL_MOVEMENT = "HorizontalMovement";
        
        /// <summary>Float parameter: forward/vertical movement input (-1 to 1).</summary>
        public const string PARAM_FORWARD_MOVEMENT = "ForwardMovement";
        
        /// <summary>Float parameter: movement speed.</summary>
        public const string PARAM_SPEED = "Speed";
        
        /// <summary>Bool parameter: is character moving.</summary>
        public const string PARAM_MOVING = "Moving";
        
        /// <summary>Int parameter: which leg is stepping (0 or 1).</summary>
        public const string PARAM_LEG_INDEX = "LegIndex";
        
        /// <summary>Float parameter: pitch angle.</summary>
        public const string PARAM_PITCH = "Pitch";
        
        /// <summary>Float parameter: yaw angle.</summary>
        public const string PARAM_YAW = "Yaw";
        
        /// <summary>Float parameter: height state (0=standing, 1=crouch, 2=prone).</summary>
        public const string PARAM_HEIGHT = "Height";
        #endregion
    }
}
