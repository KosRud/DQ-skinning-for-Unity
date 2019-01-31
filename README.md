# Dual quaternion skinning for Unity

### Features:
* GPU skinning with compute shaders (only)
* blend shape support (calculations performed in compute shader)
* works with any platform that supports compute shaders
* preserves volume with deformation (look comparison)
* zero GC allocations per frame

### Comparison:

|Gif|Difference|
|----|----|
|<img src="Screenshots/before-after.gif">|<img src="Screenshots/diff.png">|

# Warning:
You will not see any effect in edit mode.<br>
The scipt only works in play mode.<br>
If you see no effect in play mode verify that you are using the right shader.

## Unity version
The script was developed for Unity **2018.3.3.f1**. Other versions may refuse to compile `HackedStandard.shader` as the Standard shader changes from version to version and compatibility is not guaranteed.

## Performance:

During my testing the amount of time spent on actual skinning was negligible compared to the amount of time extracting `transform.position` and `transform.rotation` from every bone in the hierarchy.

As long as you are not creating hundreds of characters with complex rigs (no matter the polycount) there should be no significant performance hit.

If anyone knows how to optimize extracting position and rotation of the bones please create an [issue](https://github.com/ConstantineRudenko/DQ-skinning-for-Unity/issues) or message me on the [unity forum](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/).

## How to set up:

* Create a normal skinned character with `SkinnedMeshRenderer` component
* Add `DualQuaternionSkinner.cs` component (it will require a `MeshFilter` component)
* All materials of the mesh should use special shader to apply vertex positions

The shader is `MadCake/Material/Standard hacked for DQ skinning`

## Why do i need SkinnedMeshRenderer?

My scripts uses `SkinnedMeshRenderer` to extract an array of bones from it. Yep, that's it.<br>
The order of bones is unpredictable and does not depent on their hierarchy.<br>
Only SkinnedMeshRenderer knows it &nbsp;&nbsp; ¯\\\_(ツ)\_/¯

After extracting the bone array in `Start()` my script removes `SkinnedMeshRenderer` component as it is no longer needed.<br>
All the animations are made by the script.<br>
You can verify it in the editor after hitting play button.

## How do i use custom shaders?

Alas it's complicated.<br>
I added comments to "Standard hacked for DQ skinning" marking the alterations i made to the Standard shader.<br>
You can try to do the same with your own shader to make it work with the script.

Feel free to contact me in [this thread](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/) at unity forum if you need help.

I would also like to hear about your projects that use my script and your experience with it.

## API

[Documentation](https://constantinerudenko.github.io/Docs/DQ-skinning-for-Unity/annotated.html)

## Known bugs

When [Animator](https://docs.unity3d.com/ScriptReference/Animator.html).[cullingMode](https://docs.unity3d.com/ScriptReference/Animator-cullingMode.html) is set to anything other than **Always Animate**, it treats the mesh as if it is never visible. If you want to use animation culling, you will need to write a custom controller switching [Animator](https://docs.unity3d.com/ScriptReference/Animator.html).[cullingMode](https://docs.unity3d.com/ScriptReference/Animator-cullingMode.html) back and forth depending on object's visibility.
