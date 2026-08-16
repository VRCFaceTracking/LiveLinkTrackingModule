using System.Text;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace LiveLinkExtTrackingInterface;

public class MetaHumanPacketReader : ILiveLinkPacketReader
{
    private const int PACKET_SIZE = 584;
    private const int CURVE_OFFSET = 46;

    private float[] _shapes = new float[Enum.GetNames(typeof(MetaHumanShapes)).Length];
    
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
    }

    private void UpdateEyeExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        //TODO
    }

    private void UpdateMouthExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        //TODO
        trackingData[(int)UnifiedExpressions.JawOpen].Weight = _shapes[(int)MetaHumanShapes.JawOpen];
    }

    public void UpdateHeadData(ref UnifiedHeadData unifiedExpressionHead)
    {
        //TODO
    }
}