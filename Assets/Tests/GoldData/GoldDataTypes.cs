using System;
using System.Collections.Generic;

namespace Tests {
    [Serializable]
    public class GoldTrackData {
        public GoldMetadata metadata;
        public GoldGraph graph;
        public List<GoldSection> sections;
    }

    [Serializable]
    public class GoldMetadata {
        public string sourceFile;
        public string exportedAt;
        public string kexEditVersion;
    }

    [Serializable]
    public class GoldGraph {
        public uint rootNodeId;
        public List<uint> nodeOrder;
    }

    [Serializable]
    public class GoldSection {
        public uint nodeId;
        public string nodeType;
        public GoldVec2 position;
        public GoldInputs inputs;
        public GoldOutputs outputs;
    }

    [Serializable]
    public class GoldInputs {
        public GoldPointData anchor;
        public GoldDuration duration;
        public GoldPropertyOverrides propertyOverrides;
        public bool steering;
        public GoldCurveData curveData;
        public GoldKeyframes keyframes;
        public List<GoldPointData> sourcePath;
        public float start;
        public float end;
        public GoldPointData targetAnchor;
        public float outWeight;
        public float inWeight;
    }

    [Serializable]
    public class GoldCurveData {
        public float radius;
        public float arc;
        public float axis;
        public float leadIn;
        public float leadOut;
    }

    [Serializable]
    public class GoldOutputs {
        public int pointCount;
        public float totalLength;
        public List<GoldPointData> points;
    }

    [Serializable]
    public class GoldDuration {
        public string type;
        public float value;
    }

    [Serializable]
    public class GoldPropertyOverrides {
        public bool fixedVelocity;
        public bool heart;
        public bool friction;
        public bool resistance;
    }

    [Serializable]
    public class GoldKeyframes {
        public List<GoldKeyframe> rollSpeed;
        public List<GoldKeyframe> normalForce;
        public List<GoldKeyframe> lateralForce;
        public List<GoldKeyframe> pitchSpeed;
        public List<GoldKeyframe> yawSpeed;
        public List<GoldKeyframe> fixedVelocity;
        public List<GoldKeyframe> heart;
        public List<GoldKeyframe> friction;
        public List<GoldKeyframe> resistance;
    }

    [Serializable]
    public class GoldKeyframe {
        public uint id;
        public float time;
        public float value;
        public string inInterpolation;
        public string outInterpolation;
        public string handleType;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;
    }

    [Serializable]
    public class GoldPointData {
        public GoldVec3 heartPosition;
        public GoldVec3 direction;
        public GoldVec3 lateral;
        public GoldVec3 normal;
        public float roll;
        public float velocity;
        public float energy;
        public float normalForce;
        public float lateralForce;
        public float heartAdvance;
        public float spineAdvance;
        public float angleFromLast;
        public float pitchFromLast;
        public float yawFromLast;
        public float rollSpeed;
        public float heartArc;
        public float spineArc;
        public float frictionOrigin;
        public float heartOffset = 1.1f;
        public float friction;
        public float resistance;
        public int facing;

        // Derived energy fields for analysis
        public float effectiveFrictionDistance;
        public float kineticEnergy;
        public float gravitationalPE;
        public float frictionPE;
        public float centerY;
    }

    [Serializable]
    public class GoldVec3 {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class GoldVec2 {
        public float x;
        public float y;
    }
}
