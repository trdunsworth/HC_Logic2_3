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
    
    public partial class JC_HC_AGENCY
    {
        public JC_HC_AGENCY()
        {
            this.JC_HC_USR_SND = new HashSet<JC_HC_USR_SND>();
        }
    
        public short ID { get; set; }
        public string AG_ID { get; set; }
        public short UNITS { get; set; }
    
        public virtual ICollection<JC_HC_USR_SND> JC_HC_USR_SND { get; set; }
    }
}
