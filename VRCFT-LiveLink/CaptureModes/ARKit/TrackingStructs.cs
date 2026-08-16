namespace LiveLinkExtTrackingInterface;

// Live Link Single-Eye tracking data
public class LiveLinkTrackingDataEye
{
    public float EyeBlink;
    public float EyeLookDown;
    public float EyeLookIn;
    public float EyeLookOut;
    public float EyeLookUp;
    public float EyeSquint;
    public float EyeWide;
    public float EyePitch;
    public float EyeYaw;
    public float EyeRoll;
}

// Live Link lip tracking data
public class LiveLinkTrackingDataLowerFace
{
    public float JawForward;
    public float JawLeft;
    public float JawRight;
    public float JawOpen;
    public float MouthClose;
    public float MouthFunnel;
    public float MouthPucker;
    public float MouthLeft;
    public float MouthRight;
    public float MouthSmileLeft;
    public float MouthSmileRight;
    public float MouthFrownLeft;
    public float MouthFrownRight;
    public float MouthDimpleLeft;
    public float MouthDimpleRight;
    public float MouthStretchLeft;
    public float MouthStretchRight;
    public float MouthRollLower;
    public float MouthRollUpper;
    public float MouthShrugLower;
    public float MouthShrugUpper;
    public float MouthPressLeft;
    public float MouthPressRight;
    public float MouthLowerDownLeft;
    public float MouthLowerDownRight;
    public float MouthUpperUpLeft;
    public float MouthUpperUpRight;
    public float CheekPuff;
    public float CheekSquintLeft;
    public float CheekSquintRight;
    public float NoseSneerLeft;
    public float NoseSneerRight;
    public float TongueOut;
}

// Live Link brow tracking data
public class LiveLinkTrackingDataBrow
{
    public float BrowDownLeft;
    public float BrowDownRight;
    public float BrowInnerUp;
    public float BrowOuterUpLeft;
    public float BrowOuterUpRight;
}
    
// Live Link head tracking data
public class LiveLinkTrackingDataHead
{
    public float HeadYaw;
    public float HeadPitch;
    public float HeadRoll;
}

// All Live Link tracking data
public class LiveLinkTrackingDataStruct
{
    public LiveLinkTrackingDataEye left_eye = new LiveLinkTrackingDataEye();
    public LiveLinkTrackingDataEye right_eye = new LiveLinkTrackingDataEye();
    public LiveLinkTrackingDataLowerFace lowerface = new LiveLinkTrackingDataLowerFace();
    public LiveLinkTrackingDataBrow brow = new LiveLinkTrackingDataBrow();
    public LiveLinkTrackingDataHead head = new LiveLinkTrackingDataHead();

    //public LiveLinkTrackingDataEye getCombined()
    //{
    //    LiveLinkTrackingDataEye combined = new LiveLinkTrackingDataEye();
    //    foreach (var field in typeof(LiveLinkTrackingDataEye).GetFields(BindingFlags.Instance |
    //                                                                    BindingFlags.NonPublic |
    //                                                                    BindingFlags.Public))
    //    {
    //        object temp = combined;
    //        field.SetValue(temp, ((float)field.GetValue(left_eye) + (float)field.GetValue(right_eye)) / 2);
    //        combined = (LiveLinkTrackingDataEye)temp;
    //    }
    //    return combined;
    //}

    public void ProcessData(float[] values)
    {
        //TODO: This is inefficient. We should cache these indexes but we're already mid-upgrade to Metahuman shapes
        var arKitNames = Enum.GetNames(typeof(ArKitShapes));
            
        // For each of the eye tracking blendshapes
        foreach (var field in typeof(LiveLinkTrackingDataEye).GetFields())
        {
            string leftName = field.Name + "Left";
            string rightName = field.Name + "Right";

            field.SetValue(left_eye, values[arKitNames.IndexOf(leftName)]);
            field.SetValue(right_eye, values[arKitNames.IndexOf(rightName)]);
        }

        // For each of the lip tracking blendshapes
        foreach (var field in typeof(LiveLinkTrackingDataLowerFace).GetFields())
        {
            field.SetValue(lowerface, values[arKitNames.IndexOf(field.Name)]);
        }

        // For each of the brow tracking blendshapes
        foreach (var field in typeof(LiveLinkTrackingDataBrow).GetFields())
        {
            field.SetValue(brow, values[arKitNames.IndexOf(field.Name)]);
        }
            
        // Head datas
        foreach (var field in typeof(LiveLinkTrackingDataHead).GetFields())
        {
            field.SetValue(head, values[arKitNames.IndexOf(field.Name)]);
        }
    }
}