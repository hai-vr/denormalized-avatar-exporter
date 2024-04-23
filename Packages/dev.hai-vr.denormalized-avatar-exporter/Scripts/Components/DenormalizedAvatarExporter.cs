using UnityEngine;

namespace HaiDenormalizedAvatarExporter.Runtime
{
    [AddComponentMenu("Haï/Denormalized Avatar Exporter (Beta)")]
    public class DenormalizedAvatarExporter : MonoBehaviour
    {
        // Applicable to everything
        public GameObject avatarRoot;
        
        // Not needed in VNyan or Warudo
        public bool overrideMeta = true;
        public string metaName;
        public string metaVersion;
        public string metaAuthor;
        
        // VNyan and VSeeFace only
        public string exportFileName;

        // Warudo only
        // public bool copyToWarudoAppFolder;

        public bool doNotExecuteNDMF;
        public bool doNotExportOrBuild;
        public bool doNotDeleteWorkObjects;
        public bool doNotNormalize;
    }
}