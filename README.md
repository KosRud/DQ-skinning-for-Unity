# Dual quaternion skinning for Unity3D

### Features:
* **GPU** skinning with compute shaders (only)
* **blend shape** support (calculations performed in compute shader)
* works with **any platform that supports compute shaders**, not just Dx11
* **preserves volume** with deformation (look comparison)
* **zero GC allocations** per frame

### Comparison:

|Gif|Difference|
|----|----|
|<img src="Screenshots/before-after.gif">|<img src="Screenshots/diff.png">|


### Warning:
You will not see any effect in edit mode. The scipt only works in play mode. If you see no effect in play mode verify that you are using the right shader.

----

### Performance:

* ToDo benchmark

----

### How to set up

* Create a normal skinned character with **SkinnedMeshRenderer** component
* Add **DualQuaternionSkinner.cs** component (it will require a MeshFilter component)
* All materials of the mesh should use special shader to apply vertex positions

The shader is "**MadCake/Material/Standard hacked for DQ skinning**"

----

### Why do i need SkinnedMeshRenderer?

My scripts uses **SkinnedMeshRenderer** to extract an array of bones from it. Yep, that's it. The order of bones is unpredictable and does not depent on their hierarchy. Only SkinnedMeshRenderer knows it ¯\\\_(ツ)\_/¯

After extracting the bone array in **Awake()** my script removes **SkinnedMeshRenderer** component as it is no longer needed. All the animations are made by the script. You can verify it in the editor after hitting play button.

----

### How do i use custom shaders?

Alas it's complicated. I added comments to "Standard hacked for DQ skinning" marking the alterations i made to the Standard shader. You can try to do the same with your own shader to make it work with the script.

Feel free to contact me in [this thread](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/) at **unity forum** if you need help.
