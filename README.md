# Dual quaternion skinning for Unity3D

### Features:
* **GPU** skinning with compute shaders (only)
* **blend shape** support (calculations performed in compute shader)
* works with **any platform that supports compute shaders**
* **preserves volume** with deformation (look comparison)
* **zero GC allocations** per frame

### Comparison:

|Gif|Difference|
|----|----|
|<img src="Screenshots/before-after.gif">|<img src="Screenshots/diff.png">|


### Warning:
You will not see any effect in edit mode.<br>
The scipt only works in play mode.<br>
If you see no effect in play mode verify that you are using the right shader.

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

My scripts uses **SkinnedMeshRenderer** to extract an array of bones from it. Yep, that's it.<br>
The order of bones is unpredictable and does not depent on their hierarchy.<br>
Only SkinnedMeshRenderer knows it &nbsp;&nbsp; ¯\\\_(ツ)\_/¯

After extracting the bone array in **Start()** my script removes **SkinnedMeshRenderer** component as it is no longer needed.<br>
All the animations are made by the script.<br>
You can verify it in the editor after hitting play button.

----

### How do i use custom shaders?

Alas it's complicated.<br>
I added comments to "Standard hacked for DQ skinning" marking the alterations i made to the Standard shader.<br>
You can try to do the same with your own shader to make it work with the script.

Feel free to contact me in [this thread](https://forum.unity.com/threads/dual-quaternion-skinning-for-unity.501245/) at **unity forum** if you need help.

I would also like to hear about your projects that use my script and your experience with it.

----

### API

`class DualQuaternionSkinner : MonoBehaviour`

**public fields:**

<table>
<tr>
  <th>type</th>
  <th>Name</th>
  <th>Description</th>
</tr>
<tr>
  <td>ComputeShader</td>
  <td>shaderComputeBoneDQ</td>
  <td rowspan="3">These fields hold references to compute shaders used by the script.<br>They are assigned automatically.<br><br>Do not change them unless you know what you're doing.</td>
</tr>
<tr>
  <td>ComputeShader</td>
  <td>shaderDQBlend</td>
</tr>
<tr>
  <td>ComputeShader</td>
  <td>shaderApplyMorph</td>
</tr>
<tr>
  <td>bool</td>
  <td>started</td>
  <td>Indicates whether <b>Start()</b> method of the script has already been called.<br><br> Useful to know whether the <b>SkinnedMeshRenderer</b> component was already destroyed.</td>
</tr>
</table>

**public methods:**

<table>
<tr>
  <th width="80">type</th>
  <th width="320">Name</th>
  <th>Description</th>
</tr>
<tr>
  <td>float[ ]</td>
  <td>GetBlendShapeWeights( )</td>
  <td>Returns an array of currently applied blend shape weights sorted by blend shape index.</td>
</tr>
<tr>
  <td>void</td>
  <td>SetBlendShapeWeights(float[ ] weights)</td>
  <td>Applies blend shape weights from given array.<br><br>The length of provided array must match the number of blend shapes that currently rendered mesh has.</td>
</tr>
<tr>
  <td>float</td>
  <td>GetBlendShapeWeight(int index)</td>
  <td>Returns the currently applied weight for the blend shape with given index.</td>
</tr>
<tr>
  <td>void</td>
  <td>SetBlendShapeWeight(int index, float weight)</td>
  <td>Applies the weight for the blend shape with given index.</td>
</tr>
</table>

**public properties:**

<table>
<tr>
  <th width="200">type</th>
  <th width="200">Name</th>
  <th>Description</th>
</tr>
<tr>
  <td>UnityEngine.Mesh</td>
  <td>mesh { get; set; }</td>
  <td>The mesh being rendered.<br><br>By default the mesh will be copied from <b>SkinnedMeshRenderer</b> before this component is removed during <b>Start( )</b>.</td>
</tr>
</table>
