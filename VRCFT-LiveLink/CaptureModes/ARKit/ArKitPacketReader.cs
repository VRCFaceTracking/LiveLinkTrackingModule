using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace LiveLinkExtTrackingInterface;

public class ArKitPacketReader : ILiveLinkPacketReader
{
    private const int PACKET_SIZE = 312;
    
    private float[] _shapes = new float[Enum.GetNames(typeof(ArKitShapes)).Length];
    private LiveLinkTrackingDataStruct _latestTrackingData = new();
    
    public bool CanRead(byte[] bytes) => bytes.Length == PACKET_SIZE;

    public void Parse(byte[] bytes)
    {
        if (!CanRead(bytes)) return;
        
        // There is a bunch of static data at the beginning of the packet, it may be variable length because it includes phone name
        // So grab the last 244 bytes of the packet sent using some Linq magic, since that's where our blendshapes live
        IEnumerable<Byte> trimmedBytes = bytes.Skip(Math.Max(0, bytes.Length - 244));

        // More Linq magic, this splits our 244 bytes into 61, 4-byte chunks which we can then turn into floats
        List<List<Byte>> chunkedBytes = trimmedBytes
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / 4)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();

        // Process each float in out chunked out list
        foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
        {
            // First, reverse the list because the data will be in big endian, then convert it to a float
            item.value.Reverse();
            _shapes[item.i] = BitConverter.ToSingle(item.value.ToArray(), 0);
        }
        
        _latestTrackingData.ProcessData(_shapes);
    }

    public void UpdateUnifiedExpressions(ref UnifiedTrackingData unifiedTrackingData, bool updateEyes, bool updateExpression)
    {
        if (updateEyes)
            UpdateEyeData(ref unifiedTrackingData.Eye);
        UpdateEyeExpressions(ref unifiedTrackingData.Shapes);
        // Mouth Expression Stuff
        if (updateExpression)
            UpdateMouthExpressions(ref unifiedTrackingData.Shapes);
                
        UpdateHeadData(ref unifiedTrackingData.Head);
    }
    
    private void UpdateEyeData(ref UnifiedEyeData eye)
    {
        #region Eye Openness parsing

        // Wonder if copying the Quest Pro calculation is a good idea
        eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, _latestTrackingData.left_eye.EyeBlink +
                                                                  _latestTrackingData.left_eye.EyeBlink * _latestTrackingData.left_eye.EyeSquint));

        eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, _latestTrackingData.right_eye.EyeBlink +
                                                                   _latestTrackingData.right_eye.EyeBlink * _latestTrackingData.right_eye.EyeSquint));
        #endregion

        #region Eye Data to UnifiedEye

        var radianConst = 0.0174533f;

        //var pitch_R_mod = (float)(Math.Abs(pitch_R) + 4f * Math.Pow(Math.Abs(pitch_R) / 30f, 30f)); // curves the tail end to better accomodate actual eye pos.
        //var pitch_L_mod = (float)(Math.Abs(pitch_L) + 4f * Math.Pow(Math.Abs(pitch_L) / 30f, 30f));
        //var yaw_R_mod = (float)(Math.Abs(yaw_R) + 6f * Math.Pow(Math.Abs(yaw_R) / 27f, 18f)); // curves the tail end to better accomodate actual eye pos.
        //var yaw_L_mod = (float)(Math.Abs(yaw_L) + 6f * Math.Pow(Math.Abs(yaw_L) / 27f, 18f));

        // invert pitch, yaw because that's how VRChat likes the values?
        // assuming meowface interface outputs identically to arkit pitch/yaw
        //eye.Right.Gaze = new Vector2(_latestTrackingData.right_eye.EyeYaw * radianConst, -1 * _latestTrackingData.right_eye.EyePitch * radianConst);
        //pitch_R < 0 ? pitch_R_mod * radianConst : -1 * pitch_R_mod * radianConst,
        //yaw_R < 0 ? -1 * yaw_R_mod * radianConst : (float)yaw_R * radianConst);
        //eye.Left.Gaze = new Vector2(_latestTrackingData.left_eye.EyeYaw * radianConst, -1 * _latestTrackingData.left_eye.EyePitch * radianConst);
        //pitch_L < 0 ? pitch_L_mod * radianConst : -1 * pitch_L_mod * radianConst,
        //yaw_L < 0 ? -1 * yaw_L_mod * radianConst : (float)yaw_L * radianConst);

        // the raw values seem to be the most right 
        eye.Right.Gaze.x = _latestTrackingData.right_eye.EyeYaw;
        eye.Right.Gaze.y = -_latestTrackingData.right_eye.EyePitch;
        eye.Left.Gaze.x = _latestTrackingData.left_eye.EyeYaw;
        eye.Left.Gaze.y = -_latestTrackingData.left_eye.EyePitch;

        // Eye dilation code, automated process maybe?
        eye.Left.PupilDiameter_MM = 5f;
        eye.Right.PupilDiameter_MM = 5f;

        // Force the normalization values of Dilation to fit avg. pupil values.
        eye._minDilation = 0;
        eye._maxDilation = 10;

        #endregion
    }

    private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions)
    {

        #region Jaw Expression Set                        
        unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = _latestTrackingData.lowerface.JawOpen;
        unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = _latestTrackingData.lowerface.JawLeft;
        unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = _latestTrackingData.lowerface.JawRight;
        unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = _latestTrackingData.lowerface.JawForward;
        #endregion

        #region Mouth Expression Set   
        // using Azmidi's meowface module for reference
        unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = _latestTrackingData.lowerface.MouthClose;

        // mouth slides to the side
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = _latestTrackingData.lowerface.MouthLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = _latestTrackingData.lowerface.MouthLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = _latestTrackingData.lowerface.MouthRight;
        unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = _latestTrackingData.lowerface.MouthRight;

        unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = _latestTrackingData.lowerface.MouthSmileLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = _latestTrackingData.lowerface.MouthSmileLeft; 
        unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = _latestTrackingData.lowerface.MouthSmileRight;
        unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = _latestTrackingData.lowerface.MouthSmileRight;
        unifiedExpressions[(int)UnifiedExpressions.MouthFrownLeft].Weight = _latestTrackingData.lowerface.MouthFrownLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthFrownRight].Weight = _latestTrackingData.lowerface.MouthFrownRight;

        unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = _latestTrackingData.lowerface.MouthLowerDownLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownRight].Weight = _latestTrackingData.lowerface.MouthLowerDownRight;

        unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = _latestTrackingData.lowerface.MouthUpperUpLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = _latestTrackingData.lowerface.MouthUpperUpLeft; // apparently this is an ok map? 
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = _latestTrackingData.lowerface.MouthUpperUpRight;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = _latestTrackingData.lowerface.MouthUpperUpRight; // apparently this is an ok map? 

        unifiedExpressions[(int)UnifiedExpressions.MouthRaiserUpper].Weight = _latestTrackingData.lowerface.MouthShrugUpper;
        unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = _latestTrackingData.lowerface.MouthShrugLower;

        unifiedExpressions[(int)UnifiedExpressions.MouthDimpleLeft].Weight = _latestTrackingData.lowerface.MouthDimpleLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthDimpleRight].Weight = _latestTrackingData.lowerface.MouthDimpleRight;

        //unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = _latestTrackingData.lowerface.Mouth
        //unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = expressions[(int)FBExpression.Lip_Tightener_R];

        unifiedExpressions[(int)UnifiedExpressions.MouthPressLeft].Weight = _latestTrackingData.lowerface.MouthPressLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthPressRight].Weight = _latestTrackingData.lowerface.MouthPressRight;

        unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = _latestTrackingData.lowerface.MouthStretchLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = _latestTrackingData.lowerface.MouthStretchRight;
        #endregion

        #region Lip Expression Set   
        unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = _latestTrackingData.lowerface.MouthPucker;
        unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = _latestTrackingData.lowerface.MouthPucker;
        unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = _latestTrackingData.lowerface.MouthPucker;
        unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = _latestTrackingData.lowerface.MouthPucker;

        unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = _latestTrackingData.lowerface.MouthFunnel;
        unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = _latestTrackingData.lowerface.MouthFunnel;
        unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = _latestTrackingData.lowerface.MouthFunnel;
        unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = _latestTrackingData.lowerface.MouthFunnel;

        unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(_latestTrackingData.lowerface.MouthUpperUpLeft, 1f/6f), _latestTrackingData.lowerface.MouthRollUpper);
        unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(_latestTrackingData.lowerface.MouthUpperUpRight, 1f/6f), _latestTrackingData.lowerface.MouthRollUpper);
        unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = _latestTrackingData.lowerface.MouthRollLower;
        unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = _latestTrackingData.lowerface.MouthRollLower;
        #endregion

        #region Cheek Expression Set   
        unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = _latestTrackingData.lowerface.CheekPuff;
        unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = _latestTrackingData.lowerface.CheekPuff;
        //unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = _latestTrackingData.lowerface.CheekPuff;
        //unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = _latestTrackingData.lowerface.CheekPuff;
        unifiedExpressions[(int)UnifiedExpressions.CheekSquintLeft].Weight = _latestTrackingData.lowerface.CheekSquintLeft;
        unifiedExpressions[(int)UnifiedExpressions.CheekSquintRight].Weight = _latestTrackingData.lowerface.CheekSquintRight;
        #endregion

        #region Nose Expression Set             
        unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = _latestTrackingData.lowerface.NoseSneerLeft;
        unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = _latestTrackingData.lowerface.NoseSneerRight;
        #endregion

        #region Tongue Expression Set   
        unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = _latestTrackingData.lowerface.TongueOut;
        #endregion
    }

    public void UpdateHeadData(ref UnifiedHeadData unifiedExpressionHead)
    {
        unifiedExpressionHead.HeadPitch = -_latestTrackingData.head.HeadPitch;
        unifiedExpressionHead.HeadYaw = -_latestTrackingData.head.HeadYaw;
        unifiedExpressionHead.HeadRoll = -_latestTrackingData.head.HeadRoll;
    }
    
    private void UpdateEyeExpressions(ref UnifiedExpressionShape[] unifiedExpressions)
    {
        #region Eye Expressions Set

        unifiedExpressions[(int)UnifiedExpressions.EyeWideLeft].Weight = _latestTrackingData.left_eye.EyeWide;
        unifiedExpressions[(int)UnifiedExpressions.EyeWideRight].Weight = _latestTrackingData.right_eye.EyeWide;

        unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = _latestTrackingData.left_eye.EyeSquint;
        unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = _latestTrackingData.left_eye.EyeSquint;

        #endregion

        #region Brow Expressions Set

        unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = _latestTrackingData.brow.BrowInnerUp;
        unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpRight].Weight = _latestTrackingData.brow.BrowInnerUp;
        unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = _latestTrackingData.brow.BrowOuterUpLeft;
        unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpRight].Weight = _latestTrackingData.brow.BrowOuterUpRight;

        unifiedExpressions[(int)UnifiedExpressions.BrowPinchLeft].Weight = _latestTrackingData.brow.BrowDownLeft;
        unifiedExpressions[(int)UnifiedExpressions.BrowLowererLeft].Weight = _latestTrackingData.brow.BrowDownLeft;
        unifiedExpressions[(int)UnifiedExpressions.BrowPinchRight].Weight = _latestTrackingData.brow.BrowDownRight;
        unifiedExpressions[(int)UnifiedExpressions.BrowLowererRight].Weight = _latestTrackingData.brow.BrowDownRight;

        #endregion
    }
}