# Dual quaternion skinning for Unity

### Features:
* GPU skinning with compute shaders
* blend shape support (calculations performed in compute shader)
* works with any platform that supports compute shaders
* preserves volume with deformation (look comparison)
* zero GC allocations per frame
* original bulging compensation method

### Comparison DQ vs built-in linear:

|Gif|Difference|
|----|----|
|<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/before-after.gif" width="400">|<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/diff.png" width="400">|


### Bulging compensation demo:

<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/Leg.gif" width="600">

# Warning:
You will not see any effect in edit mode.<br>
The scipt only works in play mode.<br>
If you see no effect in play mode verify that you are using the right shader.

## Unity version
The script was tested with following Unity versions:
* **2020.1.0a13.1443** (earlier versions do not support `#pragma multi_compile` in compute shaders)

## Performance:

During my testing the amount of time spent on actual skinning was negligible compared to the amount of time extracting `localToWorldMatrix` from every bone in the hierarchy.

As long as you are not creating hundreds of characters with complex rigs (no matter the polycount) there should be no significant performance hit.

If anyone knows how to optimize extracting `localToWorldMatrix` of the bones please create an [issue](https://github.com/ConstantineRudenko/DQ-skinning-for-Unity/issues) or message me on [unity forum](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/).

**did not benchmark bulging compensation yet**

## How to set up:

* Create a skinned character with `SkinnedMeshRenderer` component
* Add `DualQuaternionSkinner.cs` component (it will require a `MeshFilter` component)
* Enable mesh Read/Write in import settings<br>

<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/Mesh import settings.png" width="463">

* All materials of the mesh should use a special shader to apply vertex positions. The shader is `MadCake/Material/Standard hacked for DQ skinning`
* In order for bulging compensation to work properly, all the character's bones must have one local axis (consistent for all bones) aligned with the limb controlled by the corresponding bone.

|Bones properly aligned|Bones not aligned|
|----|----|
|<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/bones aligned.png" width="400">|<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/bones not aligned.png" width="400">|

* Whichever local axis of the bones was aligned with the limbs must be selected in the script gui:

<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/Orientation axis.png" width="363">

If bulging is increased instead of decreased, select same axis with different direction (**X**&nbsp;=>&nbsp;**Negative&nbsp;X**)

## Common problems

The script is programmed to automatically detect common setup problems. Check out the editor:

<img src="https://raw.githubusercontent.com/ConstantineRudenko/DQ-skinning-for-Unity/master/Screenshots/Problems.png" width="363">

## Why do i need SkinnedMeshRenderer?

My scripts uses `SkinnedMeshRenderer` to extract an array of bones from it. Yep, that's it.<br>
The order of bones is unpredictable and does not depend on their hierarchy.<br>
Only SkinnedMeshRenderer knows it &nbsp;&nbsp; ¯\\\_(ツ)\_/¯

After extracting the bone array in `Start()` my script disables `SkinnedMeshRenderer` component as it is no longer needed. All the animations are performed by the script. You can verify it in the editor after hitting play button.

## How do I use custom shaders?

Alas it's complicated.<br>
I added comments to "Standard hacked for DQ skinning" marking the alterations i made to the Standard shader.<br>
You can try to do the same with your own shader to make it work with the script.

Feel free to contact me in [this thread](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/) at unity forum if you need help.

I would also like to hear about your projects that use my script and your experience with it.

## API

[Documentation](https://constantinerudenko.github.io/Docs/DQ-skinning-for-Unity/index.html)

## Future plans

* Test/improve/optimize bulging compensation
* Implement toggle for bulging compensation
* Implement simple runtime switching between **DualQuaternionSkinned** and built-in [SkinnedMeshRenderer](https://docs.unity3d.com/Manual/class-SkinnedMeshRenderer.html)
* It might make sense to group the data from all instances of the script into one batch and run the compute shaders only once per frame regardless of how many animated characters you have

## Discussion

If you have any questions, ideas, bug reports, or just want to discuss the script, you can contact me on [unity forum](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/)
