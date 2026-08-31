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
        /*
         
         Dilation no workey :/

        eye.Left.PupilDiameter_MM = Shape(MetaHumanShapes.EyePupilWideL) * 10f;
        eye.Right.PupilDiameter_MM = Shape(MetaHumanShapes.EyePupilWideR) * 10f;

        eye._minDilation = 0.0f;
        eye._maxDilation = 10.0f;*/
    }

    private void UpdateEyeExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        trackingData[(int)UnifiedExpressions.BrowPinchRight].Weight = Shape(MetaHumanShapes.BrowLateralR);
        trackingData[(int)UnifiedExpressions.BrowPinchLeft].Weight = Shape(MetaHumanShapes.BrowLateralL);

        trackingData[(int)UnifiedExpressions.BrowLowererRight].Weight = Shape(MetaHumanShapes.BrowDownR);
        trackingData[(int)UnifiedExpressions.BrowLowererLeft].Weight = Shape(MetaHumanShapes.BrowDownL);

        trackingData[(int)UnifiedExpressions.BrowInnerUpRight].Weight = Shape(MetaHumanShapes.BrowRaiseInR);
        trackingData[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = Shape(MetaHumanShapes.BrowRaiseInL);

        trackingData[(int)UnifiedExpressions.BrowOuterUpRight].Weight = Shape(MetaHumanShapes.BrowRaiseOuterR);
        trackingData[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = Shape(MetaHumanShapes.BrowRaiseOuterL);

        trackingData[(int)UnifiedExpressions.EyeWideRight].Weight = Shape(MetaHumanShapes.EyeWidenR);
        trackingData[(int)UnifiedExpressions.EyeWideLeft].Weight = Shape(MetaHumanShapes.EyeWidenL);

        // use EyeSquintInner when eye is opened, and EyeFaceScrunch when eye is closed for squint
        trackingData[(int)UnifiedExpressions.EyeSquintRight].Weight = Math.Clamp( (Shape(MetaHumanShapes.EyeSquintInnerR) * (1-(Shape(MetaHumanShapes.EyeBlinkR)))) + Shape(MetaHumanShapes.EyeFaceScrunchL)*(Shape(MetaHumanShapes.EyeBlinkR)) ,0f,1f );
        trackingData[(int)UnifiedExpressions.EyeSquintLeft].Weight = Math.Clamp((Shape(MetaHumanShapes.EyeSquintInnerL) * (1 - (Shape(MetaHumanShapes.EyeBlinkL)))) + Shape(MetaHumanShapes.EyeFaceScrunchL) * (Shape(MetaHumanShapes.EyeBlinkL)), 0f, 1f);

    }

    private void UpdateMouthExpressions(ref UnifiedExpressionShape[] trackingData)
    {
        //TODO
        trackingData[(int)UnifiedExpressions.JawOpen].Weight = Shape(MetaHumanShapes.JawOpen);
        trackingData[(int)UnifiedExpressions.JawRight].Weight = Shape(MetaHumanShapes.JawRight);
        trackingData[(int)UnifiedExpressions.JawLeft].Weight = Shape(MetaHumanShapes.JawLeft);
        trackingData[(int)UnifiedExpressions.JawBackward].Weight = Shape(MetaHumanShapes.JawBack);
        trackingData[(int)UnifiedExpressions.JawForward].Weight = Shape(MetaHumanShapes.JawFwd);
        //trackingData[(int)UnifiedExpressions.JawClench].Weight = (Shape(MetaHumanShapes.JawClenchL)+ Shape(MetaHumanShapes.JawClenchR))/2;    JawClench not part of struct



        
        trackingData[(int)UnifiedExpressions.MouthUpperRight].Weight = Shape(MetaHumanShapes.MouthRight);
        trackingData[(int)UnifiedExpressions.MouthLowerRight].Weight = Shape(MetaHumanShapes.MouthRight);
        trackingData[(int)UnifiedExpressions.MouthUpperLeft].Weight = Shape(MetaHumanShapes.MouthLeft);
        trackingData[(int)UnifiedExpressions.MouthLowerLeft].Weight = Shape(MetaHumanShapes.MouthLeft);
        /*
        For some reason these don't work as well so let's just used combined params
        trackingData[(int)UnifiedExpressions.MouthUpperRight].Weight = Shape(MetaHumanShapes.MouthUpperLipShiftRight);
        trackingData[(int)UnifiedExpressions.MouthLowerRight].Weight = Shape(MetaHumanShapes.MouthLowerLipShiftRight);
        trackingData[(int)UnifiedExpressions.MouthUpperLeft].Weight = Shape(MetaHumanShapes.MouthUpperLipShiftLeft);
        trackingData[(int)UnifiedExpressions.MouthLowerLeft].Weight = Shape(MetaHumanShapes.MouthLowerLipShiftLeft);*/

        trackingData[(int)UnifiedExpressions.MouthUpperUpRight].Weight = Shape(MetaHumanShapes.MouthUpperLipRaiseR) * (1 - (Shape(MetaHumanShapes.JawChinRaiseUR)))/*remove raiser*/ + Shape(MetaHumanShapes.MouthCornerRounderUR) + Shape(MetaHumanShapes.MouthCornerRounderDR) /*adds a little extra with JawOpen*/;
        trackingData[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = Shape(MetaHumanShapes.MouthUpperLipRaiseL) * (1- (Shape(MetaHumanShapes.JawChinRaiseUL)))/*remove raiser*/ + Shape(MetaHumanShapes.MouthCornerRounderUL) + Shape(MetaHumanShapes.MouthCornerRounderDL) /*adds a little extra with JawOpen*/;


        //MouthCornerDown not picked cause not as reliable
        trackingData[(int)UnifiedExpressions.MouthLowerDownRight].Weight = Shape(MetaHumanShapes.MouthLowerLipDepressR);
        trackingData[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = Shape(MetaHumanShapes.MouthLowerLipDepressL);


        trackingData[(int)UnifiedExpressions.CheekSquintRight].Weight = Shape(MetaHumanShapes.EyeCheekRaiseR);
        trackingData[(int)UnifiedExpressions.CheekSquintLeft].Weight = Shape(MetaHumanShapes.EyeCheekRaiseL);


        //TODO try noseWrinkleUpper and how it combines with BrowDown
        trackingData[(int)UnifiedExpressions.NoseSneerRight].Weight = Shape(MetaHumanShapes.NoseWrinkleR);
        trackingData[(int)UnifiedExpressions.NoseSneerLeft].Weight = Shape(MetaHumanShapes.NoseWrinkleL);

        //TODO NoseNasolabialDeepenR

        trackingData[(int)UnifiedExpressions.NasalConstrictLeft].Weight = Shape(MetaHumanShapes.NoseNostrilCompressL);
        trackingData[(int)UnifiedExpressions.NasalConstrictRight].Weight = Shape(MetaHumanShapes.NoseNostrilCompressR);
        trackingData[(int)UnifiedExpressions.NasalDilationRight].Weight = Shape(MetaHumanShapes.NoseNostrilDilateR);
        trackingData[(int)UnifiedExpressions.NasalDilationLeft].Weight = Shape(MetaHumanShapes.NoseNostrilDilateL);


        trackingData[(int)UnifiedExpressions.CheekPuffLeft].Weight = Shape(MetaHumanShapes.MouthCheekBlowL);
        trackingData[(int)UnifiedExpressions.CheekPuffRight].Weight = Shape(MetaHumanShapes.MouthCheekBlowR);

        trackingData[(int)UnifiedExpressions.CheekSuckRight].Weight = Shape(MetaHumanShapes.MouthCheekSuckR);
        trackingData[(int)UnifiedExpressions.LipSuckCornerRight].Weight = Shape(MetaHumanShapes.MouthCheekSuckR);//maybe something better
        trackingData[(int)UnifiedExpressions.CheekSuckLeft].Weight = Shape(MetaHumanShapes.MouthCheekSuckL);
        trackingData[(int)UnifiedExpressions.LipSuckCornerLeft].Weight = Shape(MetaHumanShapes.MouthCheekSuckL);//maybe something better


        trackingData[(int)UnifiedExpressions.MouthStretchRight].Weight = Shape(MetaHumanShapes.MouthStretchR);
        trackingData[(int)UnifiedExpressions.MouthStretchLeft].Weight = Shape(MetaHumanShapes.MouthStretchL);

        trackingData[(int)UnifiedExpressions.MouthDimpleLeft].Weight = Shape(MetaHumanShapes.MouthDimpleL);
        trackingData[(int)UnifiedExpressions.MouthDimpleRight].Weight = Shape(MetaHumanShapes.MouthDimpleR);


        trackingData[(int)UnifiedExpressions.MouthFrownRight].Weight = Shape(MetaHumanShapes.MouthCornerDepressR);
        trackingData[(int)UnifiedExpressions.MouthFrownLeft].Weight = Shape(MetaHumanShapes.MouthCornerDepressL);

        // mix with LipsTowards to make less jittery and conform more to the Pucker definition of UE pushing lips forward
        trackingData[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = (Shape(MetaHumanShapes.MouthLipsPurseUR) + Shape(MetaHumanShapes.MouthLipsTowardsUR)) / 2;
        trackingData[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = (Shape(MetaHumanShapes.MouthLipsPurseUL) + Shape(MetaHumanShapes.MouthLipsTowardsUL)) / 2;
        trackingData[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = (Shape(MetaHumanShapes.MouthLipsPurseDR) + Shape(MetaHumanShapes.MouthLipsTowardsDR)) / 2;
        trackingData[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = (Shape(MetaHumanShapes.MouthLipsPurseDL) + Shape(MetaHumanShapes.MouthLipsTowardsDL)) / 2;

        //gotta try MouthCornerSharpen
        trackingData[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = Shape(MetaHumanShapes.MouthFunnelUR);
        trackingData[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = Shape(MetaHumanShapes.MouthFunnelUL);
        trackingData[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = Shape(MetaHumanShapes.MouthFunnelDR);
        trackingData[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = Shape(MetaHumanShapes.MouthFunnelDL);


        trackingData[(int)UnifiedExpressions.MouthTightenerRight].Weight = (Shape(MetaHumanShapes.MouthLipsTightenDR) + Shape(MetaHumanShapes.MouthLipsTightenUR)) / 2;
        trackingData[(int)UnifiedExpressions.MouthTightenerLeft].Weight = (Shape(MetaHumanShapes.MouthLipsTightenDL) + Shape(MetaHumanShapes.MouthLipsTightenUL)) / 2;

        //use of MouthLipsTogether when JawOpen is 1 and MouthLipBite when JawOpen is 0
        //trackingData[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Clamp((Shape(MetaHumanShapes.JawOpen) * Shape(MetaHumanShapes.MouthLipsTogetherUR)) + ((1-(Shape(MetaHumanShapes.JawOpen))) * Shape(MetaHumanShapes.MouthUpperLipBiteR)), 0, 1);
        //trackingData[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Clamp((Shape(MetaHumanShapes.JawOpen) * Shape(MetaHumanShapes.MouthLipsTogetherUL)) + ((1-(Shape(MetaHumanShapes.JawOpen))) * Shape(MetaHumanShapes.MouthUpperLipBiteL)), 0, 1);
        //trackingData[(int)UnifiedExpressions.LipSuckLowerRight].Weight = Math.Clamp((Shape(MetaHumanShapes.JawOpen) * Shape(MetaHumanShapes.MouthLipsTogetherDR)) + ((1-(Shape(MetaHumanShapes.JawOpen))) * Shape(MetaHumanShapes.MouthLowerLipBiteR)), 0, 1);
        //trackingData[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = Math.Clamp((Shape(MetaHumanShapes.JawOpen) * Shape(MetaHumanShapes.MouthLipsTogetherDL)) + ((1-(Shape(MetaHumanShapes.JawOpen))) * Shape(MetaHumanShapes.MouthLowerLipBiteL)), 0, 1);

        //decided not to do that cause MouthLipsTogether was too jittery and triggered when Jaw was closed too much

        //trackingData[(int)UnifiedExpressions.MouthClosed].Weight = (Shape(MetaHumanShapes.MouthLipsTogetherUR) + Shape(MetaHumanShapes.MouthLipsTogetherUL) + Shape(MetaHumanShapes.MouthLipsTogetherDR) + Shape(MetaHumanShapes.MouthLipsTogetherDL)) / 4;

        //decided to pick MouthLipRollIn cause it was more reliable
        //avaliable options for lip suck: MouthLipsTogether, MouthLipBite, MouthLipRollIn
        trackingData[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Shape(MetaHumanShapes.MouthUpperLipRollInR);
        trackingData[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Shape(MetaHumanShapes.MouthUpperLipRollInL);
        trackingData[(int)UnifiedExpressions.LipSuckLowerRight].Weight = Shape(MetaHumanShapes.MouthLowerLipRollInR);
        trackingData[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = Shape(MetaHumanShapes.MouthLowerLipRollInL);


        trackingData[(int)UnifiedExpressions.MouthPressRight].Weight = (Shape(MetaHumanShapes.MouthLipsPressR));
        trackingData[(int)UnifiedExpressions.MouthPressLeft].Weight = (Shape(MetaHumanShapes.MouthLipsPressL));


        trackingData[(int)UnifiedExpressions.MouthCornerPullRight].Weight = Shape(MetaHumanShapes.MouthCornerPullR);
        trackingData[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = Shape(MetaHumanShapes.MouthCornerPullL);
        //extract slant info from the sharpcornerpull shape
        trackingData[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = Shape(MetaHumanShapes.MouthSharpCornerPullR)/ Shape(MetaHumanShapes.MouthCornerPullR);
        trackingData[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = Shape(MetaHumanShapes.MouthSharpCornerPullL)/ Shape(MetaHumanShapes.MouthCornerPullL);
        //MouthCornerUp not picked cause not as reliable
        //trackingData[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = Shape(MetaHumanShapes.MouthCornerUpR);
        //trackingData[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = Shape(MetaHumanShapes.MouthCornerUpL);
        

        trackingData[(int)UnifiedExpressions.MouthRaiserLower].Weight = (Shape(MetaHumanShapes.JawChinRaiseUR) * Shape(MetaHumanShapes.JawChinRaiseUL)) / 2;// for some reason this doesn't return anything -> (Shape(MetaHumanShapes.JawChinRaiseDR) + Shape(MetaHumanShapes.JawChinRaiseDL))/2;
        trackingData[(int)UnifiedExpressions.MouthRaiserUpper].Weight = (Shape(MetaHumanShapes.JawChinRaiseUR) * Shape(MetaHumanShapes.JawChinRaiseUL))/2;



        /*
        trackingData[(int)UnifiedExpressions.TongueOut].Weight = Shape(MetaHumanShapes.TongueOut);
        trackingData[(int)UnifiedExpressions.TongueCurlUp].Weight = Math.Clamp((Shape(MetaHumanShapes.TonguePress) + Shape(MetaHumanShapes.TongueBendUp)),0,1);
        trackingData[(int)UnifiedExpressions.TongueRoll].Weight = Shape(MetaHumanShapes.TongueRoll);
        trackingData[(int)UnifiedExpressions.TongueRoll].Weight = Shape(MetaHumanShapes.TongueFlat);
        trackingData[(int)UnifiedExpressions.TongueSquish].Weight = Shape(MetaHumanShapes.TongueNarrow);
        trackingData[(int)UnifiedExpressions.TongueBendDown].Weight = Math.Clamp((Shape(MetaHumanShapes.TongueThin) + Shape(MetaHumanShapes.TongueBendDown)),0,1);



        trackingData[(int)UnifiedExpressions.TongueUp].Weight = Shape(MetaHumanShapes.TongueUp);
        trackingData[(int)UnifiedExpressions.TongueDown].Weight = Shape(MetaHumanShapes.TongueDown);
        trackingData[(int)UnifiedExpressions.TongueRight].Weight = Shape(MetaHumanShapes.TongueRight);
        trackingData[(int)UnifiedExpressions.TongueLeft].Weight = Shape(MetaHumanShapes.TongueLeft);
        // try to combine tip params TongueTipUp, TongueTipDown, TongueTipleft, TongueTipRight

        trackingData[(int)UnifiedExpressions.TongueTwistRight].Weight = Shape(MetaHumanShapes.TongueTipRight);
        trackingData[(int)UnifiedExpressions.TongueTwistLeft].Weight = Shape(MetaHumanShapes.TongueTipLeft);
        */

        //what does MouthDown & MouthUp actually do?
    }

    public void UpdateHeadData(ref UnifiedHeadData unifiedExpressionHead)
    {
        //TODO
    }
}