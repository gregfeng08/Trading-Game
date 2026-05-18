using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class NPCAnimatorBuilder
{
    [MenuItem("Tools/Create NPC Animator Controller")]
    public static void Build()
    {
        var idleClip = LoadClipFromFBX(
            "Assets/Imports/DenysAlmaral/CityPeople-FREE/Meshes/z_animated/idle_m_1_200f.fbx");
        var walkClip = LoadClipFromFBX(
            "Assets/Imports/DenysAlmaral/CityPeople-FREE/Meshes/z_animated/locom_m_slowWalk_40f.fbx");

        if (idleClip == null)
            idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Imports/DenysAlmaral/CityPeople-FREE/Animations/idle_m_1_200f.anim");
        if (walkClip == null)
            walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Imports/DenysAlmaral/CityPeople-FREE/Animations/locom_m_slowWalk_40f.anim");

        if (idleClip == null || walkClip == null)
        {
            Debug.LogError("Could not find idle/walk clips. Check CityPeople-FREE paths.");
            return;
        }

        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/NPC_Animator.controller");
        if (existing != null)
            AssetDatabase.DeleteAsset("Assets/Animations/NPC_Animator.controller");

        var controller = AnimatorController.CreateAnimatorControllerAtPath(
            "Assets/Animations/NPC_Animator.controller");

        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);

        var rootStateMachine = controller.layers[0].stateMachine;

        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        var walkState = rootStateMachine.AddState("Walk");
        walkState.motion = walkClip;

        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
        toWalk.hasExitTime = false;
        toWalk.duration = 0.15f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;

        AssetDatabase.SaveAssets();
        Debug.Log($"NPC_Animator.controller created. Idle={idleClip.name}, Walk={walkClip.name}");
    }

    private static AnimationClip LoadClipFromFBX(string fbxPath)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (assets == null) return null;

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }
        return null;
    }
}
