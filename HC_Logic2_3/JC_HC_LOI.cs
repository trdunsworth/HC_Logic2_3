//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace HC_Logic2_3
{
    using System;
    using System.Collections.Generic;
    
    public partial class JC_HC_LOI
    {
        public JC_HC_LOI()
        {
            this.JC_HC_USR_SND = new HashSet<JC_HC_USR_SND>();
        }
    
        public int ID { get; set; }
        public string HNDR_BLCK { get; set; }
        public string LOI_GRP_ID { get; set; }
        public string ZIP { get; set; }
        public string EFEANME { get; set; }
        public string ESTNUM { get; set; }
        public string EDIRPRE { get; set; }
        public string EFEATYP { get; set; }
        public string COMMON_NAME { get; set; }
        public string CITY { get; set; }
        public string ACTIVE { get; set; }
        public string ADDRESS { get; set; }
        public Nullable<int> ESZ { get; set; }
    
        public virtual ICollection<JC_HC_USR_SND> JC_HC_USR_SND { get; set; }
    }
}