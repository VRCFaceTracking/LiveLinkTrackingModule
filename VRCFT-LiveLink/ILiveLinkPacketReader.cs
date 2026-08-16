using VRCFaceTracking.Core.Params.Data;

namespace LiveLinkExtTrackingInterface;

public interface ILiveLinkPacketReader
{
    bool CanRead(byte[] bytes);
    void Parse(byte[] bytes);
    void UpdateUnifiedExpressions(ref UnifiedTrackingData trackingData, bool updateEyes, bool updateExpression);
}