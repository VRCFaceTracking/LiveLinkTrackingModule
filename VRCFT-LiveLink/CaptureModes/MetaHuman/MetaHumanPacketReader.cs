using System.Text;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace LiveLinkExtTrackingInterface;

public class MetaHumanPacketReader : ILiveLinkPacketReader
{
    private const int PACKET_SIZE = 584;
    private const int CURVE_OFFSET = 46;

    private float[] _shapes = new float[Enum.GetNames(typeof(MetaHumanShapes)).Length];
    private float Shape(MetaHumanShapes shape) => _shapes[(int)shape];
    
    public bool CanRead(byte[] buffer) => buffer.Length == PACKET_SIZE;
    
    public void Parse(byte[] buffer)
    {
        // Referenced RamesTheGeneric/LiveLinkFace_MH_CTRL/blob/main/mha_receiver.py heavily for this

        if (!CanRead(buffer)) return;

        var version = BitConverter.ToUInt16(buffer);
        var uuidLength = BitConverter.ToUInt16(buffer, 2);
        if (uuidLength == 0 || uuidLength > 64) return;

        var uuidStr = Encoding.ASCII.GetString(buffer, 4, uuidLength);
        var seq = buffer[uuidLength + 4];
        var tc = buffer.AsSpan(5 + uuidLength, 5);
        string tcStr = $"{tc[0]:D2}:{tc[1]:D2}:{tc[2]:D2}:{tc[3]:D2}.{tc[4]:D2}";

        for (int i = 0; i < Enum.GetNames(typeof(MetaHumanShapes)).Length; i++)
        {
            _shapes[i] = (float)(BitConverter.ToUInt16(buffer, CURVE_OFFSET+i*2) / 65535.0);
        }
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
        //TODO
        eye.Left.Openness = 1.0f - Math.Max(0,
            Math.Min(1,
                Shape(MetaHumanShapes.EyeBlinkL) +
                Shape(MetaHumanShapes.EyeBlinkL) * Shape(MetaHumanShapes.EyeSquintInnerL)));
        
        eye.Right.Openness = 1.0f - Math.Max(0,
            Math.Min(1,
                Shape(MetaHumanShapes.EyeBlinkR) +
                Shape(MetaHumanShapes.EyeBlinkR) * Shape(MetaHumanShapes.EyeSquintInnerR)));

        eye.Left.Gaze.x = Shape(MetaHumanShapes.EyeLookRightL) + -Shape(MetaHumanShapes.EyeLookLeftL);
        eye.Left.Gaze.y = Shape(MetaHumanShapes.EyeLookUpL) + -Shape(MetaHumanShapes.EyeLookDownL);
        eye.Right.Gaze.x = Shape(MetaHumanShapes.EyeLookRightR) + -Shape(MetaHumanShapes.EyeLookLeftR);
        eye.Right.Gaze.y = Shape(MetaHumanShapes.EyeLookUpR) + -Shape(MetaHumanShapes.EyeLookDownR);

        /*eye.Left.PupilDiameter_MM = Shape(MetaHumanShapes.EyePupilWideL) * 10f;
        eye.Right.PupilDiameter_MM = Shape(MetaHumanShapes.EyePupilWideR) * 10f;

        eye._minDilation = 0.0f;
        eye._maxDilation = 10.0f;*/
    }

    private void UpdateEyeExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        //TODO
    }

    private void UpdateMouthExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        //TODO
        trackingData[(int)UnifiedExpressions.JawOpen].Weight = Shape(MetaHumanShapes.JawOpen);
    }

    public void UpdateHeadData(ref UnifiedHeadData unifiedExpressionHead)
    {
        //TODO
    }
}