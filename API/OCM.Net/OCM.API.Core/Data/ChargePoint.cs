//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace OCM.Core.Data
{
    using System;
    using System.Collections.Generic;
    
    public partial class ChargePoint
    {
        public ChargePoint()
        {
            this.ChildChargePoints = new HashSet<ChargePoint>();
            this.Connections = new HashSet<ConnectionInfo>();
            this.MetadataValues = new HashSet<MetadataValue>();
            this.UserComments = new HashSet<UserComment>();
            this.MediaItems = new HashSet<MediaItem>();
        }
    
        public int ID { get; set; }
        public string UUID { get; set; }
        public Nullable<int> ParentChargePointID { get; set; }
        public int DataProviderID { get; set; }
        public string DataProvidersReference { get; set; }
        public Nullable<int> OperatorID { get; set; }
        public string OperatorsReference { get; set; }
        public Nullable<int> UsageTypeID { get; set; }
        public Nullable<int> AddressInfoID { get; set; }
        public Nullable<int> NumberOfPoints { get; set; }
        public string GeneralComments { get; set; }
        public Nullable<System.DateTime> DatePlanned { get; set; }
        public Nullable<System.DateTime> DateLastConfirmed { get; set; }
        public Nullable<int> StatusTypeID { get; set; }
        public Nullable<System.DateTime> DateLastStatusUpdate { get; set; }
        public Nullable<int> DataQualityLevel { get; set; }
        public Nullable<System.DateTime> DateCreated { get; set; }
        public Nullable<int> SubmissionStatusTypeID { get; set; }
        public string UsageCost { get; set; }
        public Nullable<short> LevelOfDetail { get; set; }
    
        public virtual AddressInfo AddressInfo { get; set; }
        public virtual ICollection<ChargePoint> ChildChargePoints { get; set; }
        public virtual ChargePoint ParentChargePoint { get; set; }
        public virtual DataProvider DataProvider { get; set; }
        public virtual Operator Operator { get; set; }
        public virtual StatusType StatusType { get; set; }
        public virtual SubmissionStatusType SubmissionStatusType { get; set; }
        public virtual UsageType UsageType { get; set; }
        public virtual ICollection<ConnectionInfo> Connections { get; set; }
        public virtual ICollection<MetadataValue> MetadataValues { get; set; }
        public virtual ICollection<UserComment> UserComments { get; set; }
        public virtual ICollection<MediaItem> MediaItems { get; set; }
    }
}
