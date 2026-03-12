# Setting Up Opsive FreeClimb Animations

## Step 1: Wait for Unity Import

After copying the FBX files, Unity needs to import them. You should see new animation clips appear at:
`Assets/Art/Animations/Opsive/`

## Step 2: Configure FBX Import Settings

For **each FBX file** in `Assets/Art/Animations/Opsive/`:

1. Select the FBX in Project window
2. In Inspector, go to **Rig** tab:
   - **Animation Type**: Humanoid
   - **Avatar Definition**: Create From This Model
3. Click **Apply**
4. Go to **Animation** tab:
   - Check **Loop Time** for: `FreeClimbIdle`, `FreeClimbVertical`, `FreeClimbHorizontal`, `FreeClimbDiagonal`
   - Uncheck **Loop Time** for mount/dismount clips
5. Click **Apply**

## Step 3: Add States to Animator Controller

Open `Assets/Art/Models/WarrokAnimationController.controller` in the Animator window.

### Find the "Climb Layer" (already exists)

In the Layers panel, you should see "Climb Layer". Select it.

### Add New States

Right-click in the graph → Create State → Empty, and create these states:

| State Name | Motion (FBX Clip) |
|------------|-------------------|
| FreeClimb_Idle | FreeClimbIdle.fbx → clip |
| FreeClimb_Up | FreeClimbVertical.fbx → clip |
| FreeClimb_Horizontal | FreeClimbHorizontal.fbx → clip |
| FreeClimb_Diagonal | FreeClimbDiagonal.fbx → clip |
| FreeClimb_BottomMount | FreeClimbBottomMount.fbx → clip |
| FreeClimb_TopMount | FreeClimbTopMount.fbx → clip |
| FreeClimb_BottomDismount | FreeClimbBottomDismount.fbx → clip |
| FreeClimb_TopDismount | FreeClimbTopDismount.fbx → clip |

### Create Blend Tree (Recommended)

Instead of individual states, create a 2D Blend Tree for climbing movement:

1. Right-click → Create State → From New Blend Tree
2. Name it "FreeClimb_Movement"
3. Double-click to edit:
   - **Blend Type**: 2D Simple Directional
   - **Parameters**: ClimbHorizontal (X), ClimbSpeed (Y)
4. Add Motion Fields:
   - Position (0, 0): FreeClimbIdle
   - Position (0, 1): FreeClimbVertical
   - Position (-1, 0): FreeClimbHorizontal (mirrored)
   - Position (1, 0): FreeClimbHorizontal
   - Position (0.7, 0.7): FreeClimbDiagonal
   - Position (-0.7, 0.7): FreeClimbDiagonal (mirrored)

## Step 4: Create Transitions

### Entry Transition
- From **Any State** → **FreeClimb_BottomMount**
- Condition: `IsClimbing` == true

### Mount Complete
- From **FreeClimb_BottomMount** → **FreeClimb_Movement** (Blend Tree)
- Has Exit Time: true, Exit Time: 0.9

### Exit Transition
- From **FreeClimb_Movement** → **FreeClimb_BottomDismount**
- Condition: `IsClimbing` == false, `ClimbProgress` < 0.2

- From **FreeClimb_Movement** → **FreeClimb_TopDismount**
- Condition: `IsClimbing` == false, `ClimbProgress` > 0.8

## Step 5: Test in Play Mode

1. Start climbing
2. The character should:
   - Play mount animation
   - Blend between idle/vertical/horizontal based on input
   - Play dismount when releasing

## Troubleshooting

### Animations don't play
- Check that `IsClimbing` parameter is being set in `ClimbAnimatorBridge.cs`
- Verify the Animator layer weight for "Climb Layer" is 1

### Character T-poses during climb
- The FBX rig doesn't match your character
- Try: Model → Avatar Definition → Copy From Other Avatar → select your character's avatar
