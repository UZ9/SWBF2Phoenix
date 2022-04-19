using System.Collections.Generic;
using UnityEngine;
using LibSWBF2.Wrappers;
using LibSWBF2.Utils;


public static class PhxAnimLoader
{
    public const float BakeFPS = 120f;

    public static Container Con;

    static Dictionary<uint, PhxAnimClip> ClipDB = new Dictionary<uint, PhxAnimClip>();

    static readonly uint Hash_dummyroot = HashUtils.GetCRC("dummyroot");

    static readonly float[] ComponentMultipliers = 
    {
        -1.0f,
         1.0f,
         1.0f,
        -1.0f,
        -1.0f,
         1.0f,
         1.0f  
    };

    static readonly string[] HUMANM_BANKS =
    {
        "human_0",
        "human_1",
        "human_2",
        "human_3",
        "human_4",
        "human_sabre"
    };

    public static void ClearDB()
    {
        ClipDB.Clear();
    }

    public static bool Exists(string bankName, string animName)
    {
        if (bankName == "human")
        {
            return Exists(HUMANM_BANKS, animName);
        }
        return Exists(bankName, HashUtils.GetCRC(animName));
    }

    public static bool Exists(string[] animBanks, string animName)
    {
        for (int i = 0; i < animBanks.Length; ++i)
        {
            if (Exists(animBanks[i], animName))
            {
                return true;
            }
        }
        return false;
    }

    public static bool Exists(string bankName, uint animNameCRC)
    {
        uint animID = HashUtils.GetCRC(bankName) * animNameCRC;
        if (ClipDB.TryGetValue(animID, out PhxAnimClip clip))
        {
            return true;
        }

        AnimationBank bank = Con.Get<AnimationBank>(bankName);
        if (bank == null)
        {
            return false;
        }

        return bank.GetAnimationMetadata(animNameCRC, out _, out _);
    }

    public static PhxAnimClip Import(string bankName, string animName)
    {
        if (bankName == "human")
        {
            return Import(HUMANM_BANKS, animName);
        }
        return Import(bankName, HashUtils.GetCRC(animName), animName);
    }

    public static PhxAnimClip Import(string[] animBanks, string animName)
    {
        PhxAnimClip clip = PhxAnimClip.None;
        for (int i = 0; i < animBanks.Length; ++i)
        {
            if (Exists(animBanks[i], animName))
            {
                clip = Import(animBanks[i], animName);
                Debug.Assert(clip.Clip.IsValid());
                break;
            }
        }
        return clip;
    }

    public static PhxAnimClip Import(string bankName, uint animNameCRC, string clipNameOverride=null)
    {
        uint animID = HashUtils.GetCRC(bankName) * animNameCRC;
        if (ClipDB.TryGetValue(animID, out PhxAnimClip clip))
        {
            return clip;
        }

        AnimationBank bank = Con.Get<AnimationBank>(bankName);
        if (bank == null)
        {
            //Debug.LogError($"Cannot find AnimationBank '{bankName}'!");
            return PhxAnimClip.None;
        }

        if (!bank.GetAnimationMetadata(animNameCRC, out int numFrames, out int numBones))
        {
            //Debug.LogError($"Cannot find Animation '{animNameCRC}' in AnimationBank '{bankName}'!");
            return PhxAnimClip.None;
        }

        CraSourceClip srcClip = new CraSourceClip();
        srcClip.Name = string.IsNullOrEmpty(clipNameOverride) ? animNameCRC.ToString() : clipNameOverride;

        uint[] boneCRCs = bank.GetBoneCRCs(animNameCRC);

        List<CraBone> bones = new List<CraBone>();
        for (int i = 0; i < boneCRCs.Length; ++i)
        {
            if (boneCRCs[i] == Hash_dummyroot)
            {
                Debug.Assert(clip.RootMotionFrames == null);
                clip.RootMotionFrames = new PhxTransform[numFrames];

                for (int j = 0; j < 7; ++j)
                {
                    if (!bank.GetCurve(animNameCRC, boneCRCs[i], (uint)j, out ushort[] indices, out float[] values))
                    {
                        Debug.LogWarning($"Getting curve in animation '{animNameCRC}' of bone '{boneCRCs[i]}' at component '{j}' failed!");
                        continue;
                    }

                    Debug.Assert(indices.Length == values.Length);

                    for (int k = 0; k < indices.Length; ++k)
                    {
                        int index = indices[k];
                        float time = index < numFrames ? index / 30.0f : numFrames / 30.0f;
                        float value = values[k] * ComponentMultipliers[j];

                        if (j < 4)
                        {
                            clip.RootMotionFrames[k].Rotation[j] = value;
                        }
                        else
                        {
                            clip.RootMotionFrames[k].Position[j - 4] = value;
                        }
                    }
                }
            }
            else
            {
                CraBone bone = new CraBone();
                bone.BoneHash = (int)boneCRCs[i];
                bone.Curve = new CraSourceTransformCurve();

                for (int j = 0; j < 7; ++j)
                {
                    if (!bank.GetCurve(animNameCRC, boneCRCs[i], (uint)j, out ushort[] indices, out float[] values))
                    {
                        Debug.LogWarning($"Getting curve in animation '{animNameCRC}' of bone '{boneCRCs[i]}' at component '{j}' failed!");
                        continue;
                    }

                    Debug.Assert(indices.Length == values.Length);

                    for (int k = 0; k < indices.Length; ++k)
                    {
                        int index = indices[k];
                        float time = index < numFrames ? index / 30.0f : numFrames / 30.0f;
                        float value = values[k] * ComponentMultipliers[j];

                        bone.Curve.Curves[j].EditKeys.Add(new CraKey(time, value));
                    }
                }

                bones.Add(bone);
            }

        }
        srcClip.SetBones(bones.ToArray());
        srcClip.Bake(BakeFPS);
        clip.Clip = CraClip.CreateNew(srcClip);
        ClipDB.Add(animID, clip);
        return clip;
    }
}
