using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace HC_Logic2_3
{
  /// <summary>
  /// Hotcalls is, currently, a two-piece program which monitors the JCSOARCH CAD database for events which
  /// reach a collection of triggers. (Location, Call Type and Subtype, or Number of Arrived Units)
  /// The two parts are the poller which queries the IPS created tables and then consolidates that data into 
  /// JCSO created tables for evaluation and the logic which then evaluates the data. 
  /// If one or more of the triggers is matched, the logic program will create and send an email to subscribers
  /// using the county's SMTPRelayOut Exchange Server.
  /// The Poller should be started first and then the Logic started roughly 60 seconds later.
  /// Both programs are run by the Windows Server 2008 Task Scheduler on a 2 minute rotation roughly 60 secs apart.
  /// Both are installed and scheduled via the local Compgrp account.
  /// The poller and logic program files live in the following path:
  /// C:\Users\jcsoadmin\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Poller -or- Logic
  /// If there are errors, they are collected and written to files in the C:\temp folder. 
  /// Both programs also send out emails to Carter Wetherington and Tony Dunsworth for any errors for rapid response.
  /// This version was created in Visual Studio 2013 using the .NET 4.5.1 framework and the C#5.0 language specifications.
  /// Copyright: 2013-2014, Johnson County Sheriff's Office, all rights reserved.
  /// The first run of code in Version 2 in debug mode on 2014-04-09 was successful
  /// </summary>
  // Program: Hot Calls Logic Version 2 Write 3
  // Author: Tony Dunsworth
  // Date Created: 2014-08-18 (Version 2.0.3.1)
  // Current To-Do List 2014-08-18
  // 1. Review Location of Interest integration and the configuration. (Need to make minor algoirthmic adjustments)
  // 2. Looking at serializing some XML for RSS feed? 
  // 3. Review  LINQ coding for optimization.
  // 4. Investigate moving from EF to NHibernate for additional capabilities and code weight.
  // 5. If staying with EF, move to EF 6 when supported by odp.net

  class Program
  {
    public static DateTime compTime = DateTime.Now.AddMinutes(-60);
    public static string formatString = "yyyyMMddHHmmss";
    public static int batch = 150;
    public static char[] delimiters = new char[] { ';' };
    public static List<string> CivEmails = new List<string>();
    public static List<string> LeoEmails = new List<string>();
    public static MailAddress fromAddy = new MailAddress("jcso.hotcalls@jocogov.org", "HOT CALLS");
    public static string[] callComms;
    static void Main(string[] args)
    {
      // Main function which invokes the timer, scheduled to run every 120 seconds, which runs each individual method
      // Main start and stops with a time stamp for visual verification in debug mode
      // The try/catch block was added to allow any errors not caught elsewhere to be caught and mailed to the admins.
      try
      {
        Console.WriteLine("Evaluator has started at " + DateTime.Now.ToString("G"));
        LoiEval();
        TypeEval();
        CountEval();
        Console.WriteLine("Evaluator has ended at " + DateTime.Now.ToString("G"));
      }
      catch (Exception ex)
      {
        MailException(ex);
        LogException(ex);
      }
    }

    private static void LoiEval()
    {
      // This function reads the rows of the jc_hc_curent or curent_dev table and compares the addresses returned 
      // to the jc_hc_loi or loi_dev table for noted locations of interest (Schools, county buildings, hospitals, etc.)
      // A Console.WriteLine is written here for debugging purposes, it is to be commented out in the live version or the running test
      // Console.WriteLine("LoiEval entered at " + DateTime.Now.ToString("G"));
      // The lTime variable is set for 60 minutes prior to the run time.
      // Moved the time variables up with the connection string as a public accessible variable.
      // DateTime lTime = DateTime.Now.AddMinutes(-60);
      // Select the basic fields needed from the jc_hc_curent table to be compared.
      hclogic LoiEntity = new hclogic();
      var allCalls = from c in LoiEntity.JC_HC_CURENT
                     where c.EFEANME != null
                     select new
                     {
                       c.EID,
                       c.NUM_1,
                       c.TYCOD,
                       c.SUB_TYCOD,
                       c.ESTNUM,
                       c.EDIRPRE,
                       c.EFEANME,
                       c.EFEATYP,
                       c.AG_ID,
                       c.AD_TS,
                       c.ESZ,
                       c.COMMENTS,
                       c.LOI_EVAL,
                       c.LOI_SENT,
                       c.UNIT_COUNT
                     };
      foreach (var call in allCalls)
      {
        int Eid = call.EID;
        string Num1 = call.NUM_1;
        string Tycod = call.TYCOD;
        string Subcod = call.SUB_TYCOD;
        string Estnum = call.ESTNUM;
        string Edirpre = call.EDIRPRE;
        string Efeanme = call.EFEANME;
        string Efeatyp = call.EFEATYP;
        string Agid = call.AG_ID;
        string Adts = call.AD_TS;
        int? Esz = call.ESZ;
        string Comments = call.COMMENTS;
        string Leval = call.LOI_EVAL;
        string Lsent = call.LOI_SENT;
        int? UnitsArv = call.UNIT_COUNT;

        if (Leval == "F")
        {
          DateTime lTime = DateTime.ParseExact(Adts.Substring(0, 14), formatString, null);
          int result = DateTime.Compare(lTime, compTime);
          if (result > 0)
          {
            var loiLists = from l in LoiEntity.JC_HC_LOI
                           where l.ACTIVE == "T"
                           select new 
                           {
                             l.ID, 
                             l.ESTNUM, 
                             l.EDIRPRE, 
                             l.EFEANME, 
                             l.EFEATYP, 
                             l.HNDR_BLCK, 
                             l.LOI_GRP_ID
                           };
            foreach (var loi in loiLists)
            {
              int lId = loi.ID;
              string lEstnum = loi.ESTNUM;
              string lEdirpre = loi.EDIRPRE;
              string lEfeanme = loi.EFEANME;
              string lEfeatyp = loi.EFEATYP;
              string lBlock = loi.HNDR_BLCK;
              string lGroup = loi.LOI_GRP_ID;

              if (Estnum == lEstnum && Edirpre == lEdirpre && Efeanme == lEfeanme && Efeatyp == lEfeatyp)
              {
                try
                {
                  callComms = Comments.Split(delimiters);
                  string dispTime = Adts.Substring(8, 2) + ":" + Adts.Substring(10, 2);

                  var loiUsers = from u in LoiEntity.JC_HC_USERS
                                 where (from g in LoiEntity.JC_HC_USR_SND
                                        where g.LOI_ID == lId || g.GRP_ID == lGroup
                                        select new { g.USR_ID }).Equals(u.ID) && u.OOF == "F"
                                 select new { u.EMAIL, u.LEO };
                  foreach (var lUsr in loiUsers)
                  {
                    string lEmail = lUsr.EMAIL;
                    string lLeo = lUsr.LEO;

                    if (lLeo == "F")
                    {
                      CivEmails.Add(lEmail);
                    }
                    else
                    {
                      LeoEmails.Add(lEmail);
                    }

                    if (CivEmails.Count > 0)
                    {
                      for (int i = 0; i < CivEmails.Count; i += batch)
                      {
                        using (MailMessage msg = new MailMessage())
                        {
                          msg.From = new MailAddress("jcso.loicall@jocogov.org", "LOI CALL");
                          for (int j = 1; (j < (i + batch)) && (j < CivEmails.Count); ++j)
                          {
                            msg.Bcc.Add(new MailAddress(CivEmails[j].Trim()));
                          }
                          msg.Subject = Agid.Trim() + " LOI CALL";
                          msg.Body = "Location: " + lBlock.Trim() + " Block of " + Edirpre.Trim() + " " + Efeanme.Trim() + " " + Efeatyp.Trim() + "\n";
                          msg.Body += "Agency: " + Agid.Trim() + "\tUnits Arrived: " + UnitsArv + "\tEvent No: " + Num1.Trim() + "\n";
                          msg.Body += "Time Dispatched: " + dispTime.Trim() + "\tNature" + Tycod.Trim() + " / " + Subcod.Trim();
                          SmtpClient post = new SmtpClient();
                          post.Send(msg);
                        }
                      }
                    }
                    if (LeoEmails.Count > 0)
                    {
                      for (int i = 0; i < LeoEmails.Count; i += batch)
                      {
                        using (MailMessage msg = new MailMessage())
                        {
                          msg.From = new MailAddress("jcso.loicall@jocogov.org", "LOI CALL");
                          for (int j = i; (j < (i + batch)) && (j < LeoEmails.Count); ++j)
                          {
                            msg.Bcc.Add(new MailAddress(CivEmails[j].Trim()));
                          }
                          msg.Subject = Agid.Trim() + " LOI CALL";
                          msg.Body = "Location: " + Estnum.Trim() + " " + Edirpre.Trim() + " " + Efeanme.Trim() + " " + Efeatyp.Trim() + "\n";
                          msg.Body += "Agency: " + Agid.Trim() + "\tUnits Arrived: " + UnitsArv + "\tEvent No: " + Num1.Trim() + "\n";
                          msg.Body += "Time Dispatched: " + dispTime.Trim() + "\tNature" + Tycod.Trim() + " / " + Subcod.Trim() + "\n\n";
                          foreach (string s in callComms)
                          {
                            msg.Body += s.Trim() + "\n";
                          }
                          SmtpClient post = new SmtpClient();
                          post.Send(msg);
                        }
                      }
                    }
                  }
                }
                catch (OracleException ox)
                {
                  MailException(ox);
                  LogException(ox);
                }
                catch (Exception ex)
                {
                  MailException(ex);
                  LogException(ex);
                }
                finally
                {
                  JC_HC_CURENT update = (from u in LoiEntity.JC_HC_CURENT
                                         where u.EID == Eid && u.NUM_1 == Num1 && u.AD_TS == Adts
                                         select u).FirstOrDefault();
                  if (update != null)
                  {
                    update.LOI_EVAL = "T";
                    update.LOI_SENT = "T";
                  }

                  JC_HC_SENT sent = new JC_HC_SENT();
                  sent.EID = Eid;
                  sent.AG_ID = Agid;
                  sent.TYCOD = Tycod;
                  sent.SUB_TYCOD = Subcod;
                  sent.SENT_DT = DateTime.Now.ToString("yyyyMMddHHmmss");
                  sent.EMAIL_SENT = Comments;
                  sent.NUM_1 = Num1;

                  LoiEntity.JC_HC_SENT.Add(sent);
                  LoiEntity.SaveChanges();
                }
              }
            }
          }
          else
          {
            var lDefault = (from d in LoiEntity.JC_HC_CURENT
                            where d.LOI_EVAL == "F"
                            select d).First();
            lDefault.LOI_EVAL = "T";
            LoiEntity.SaveChanges();
          }
        }
      }
      // Console.WriteLine("LoiEval exited at " + DateTime.Now.ToString("G"));
      return;
    }

    // This function will compare the Primary and Secondary Call Types to the types or types_dev tables
    // to see if the call is automatically "hot" by the type of call and the agnecy's request.
    // Since many of the things here are of similar structure to the function above, the comments will be a little more sparse.
    // The emailing portion has been duplicated in this and the following function to account for either full addresses
    // or cross streets if the actual address has not been entered into the system at the time this becomes a hot call.
    private static void TypeEval()
    {
      // Write in a Console.Writeline for debugging purposes. This is removed or commented out in live production code so it is 
      // cleaner and more efficient when running.
      // All DateTime strings are being reformatted to more user friendly versions.
      // Console.WriteLine("TypeDevEval has entered at: " + DateTime.Now.ToString("G"));
      // DateTime tTime = DateTime.Now.AddMinutes(-60);
      hclogic TypeEntity = new hclogic();
      var typeCalls = from c in TypeEntity.JC_HC_CURENT
                     where c.TYCOD != null
                     select new
                     {
                       c.EID,
                       c.NUM_1,
                       c.TYCOD,
                       c.SUB_TYCOD,
                       c.ESTNUM,
                       c.EDIRPRE,
                       c.EFEANME,
                       c.EFEATYP,
                       c.AG_ID,
                       c.AD_TS,
                       c.XSTREET1,
                       c.XSTREET2,
                       c.ESZ,
                       c.COMMENTS,
                       c.TYPE_EVAL,
                       c.HC_SENT,
                       c.UNIT_COUNT
                     };
      foreach (var call in typeCalls)
      {
        int Eid = call.EID;
        string Num1 = call.NUM_1;
        string Tycod = call.TYCOD;
        string Subcod = call.SUB_TYCOD;
        string Estnum = call.ESTNUM;
        string Edirpre = call.EDIRPRE;
        string Efeanme = call.EFEANME;
        string Efeatyp = call.EFEATYP;
        string Agid = call.AG_ID;
        string Adts = call.AD_TS;
        string Xstr1 = call.XSTREET1;
        string Xstr2 = call.XSTREET2;
        int? Esz = call.ESZ;
        string Comments = call.COMMENTS;
        string Teval = call.TYPE_EVAL;
        string Tsent = call.HC_SENT;
        int? UnitsArv = call.UNIT_COUNT;

        if (UnitsArv == null)
        {
          UnitsArv = 0;
        }

        string dispTime = Adts.Substring(8, 2) + ":" + Adts.Substring(10, 2);

        if (Teval == "F")
        {
          DateTime tTime = DateTime.ParseExact(Adts.Substring(0, 14), formatString, null);
          int result = DateTime.Compare(tTime, compTime);
          if (result > 0)
          {
            var typeLists = from t in TypeEntity.JC_HC_TYPES
                            where t.ALWYS_SND == "T" && t.NEVR_SND == "F"
                            select new
                            {
                              t.ID,
                              t.TYCOD,
                              t.SUB_TYCOD,
                              t.AGENCY
                            };
            foreach (var type in typeLists)
            {
              int tId = type.ID;
              string tTycod = type.TYCOD;
              string tSubcod = type.SUB_TYCOD;
              string tAgid = type.AGENCY;

              var typeUsers = from u in TypeEntity.JC_HC_USERS
                              where (from g in TypeEntity.JC_HC_USR_SND
                                     where (from a in TypeEntity.JC_HC_AGENCY
                                            where a.AG_ID == Agid || a.ID == 17
                                            select new { a.ID }).Equals(g.AGY_ID)
                                     select new { g.USR_ID }).Equals(u.ID) && u.OOF == "F"
                              select new { u.EMAIL, u.LEO };

              if (Tycod == tTycod && Agid == tAgid && Subcod == tSubcod)
              {
                if (Efeanme != null)
                {
                  try
                  {
                    foreach (var user in typeUsers)
                    {
                      string tEmail = user.EMAIL;
                      string tLeo = user.LEO;

                      if (tLeo == "F")
                      {
                        CivEmails.Add(tEmail);
                      }
                      else
                      {
                        LeoEmails.Add(tEmail);
                      }

                      if (CivEmails.Count > 0)
                      {
                        CivEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv);
                      }

                      if (LeoEmails.Count > 0)
                      {
                        LeoEmail(Agid, Tycod, Subcod, Estnum, Edirpre, Efeanme, Efeatyp, dispTime, Num1, UnitsArv, callComms);
                      }
                    }
                  }
                  catch (OracleException ox)
                  {
                    MailException(ox);
                    LogException(ox);
                  }
                  catch (Exception ex)
                  {
                    MailException(ex);
                    LogException(ex);
                  }
                  finally
                  {
                    JC_HC_CURENT tUpdate = (from u in TypeEntity.JC_HC_CURENT
                                            where u.EID == Eid && u.NUM_1 == Num1 && u.AD_TS == Adts
                                            select u).FirstOrDefault();
                    if (tUpdate != null)
                    {
                      tUpdate.TYPE_EVAL = "T";
                      tUpdate.HC_SENT = "T";
                    }

                    JC_HC_SENT tSent = new JC_HC_SENT();
                    tSent.EID = Eid;
                    tSent.AG_ID = Agid;
                    tSent.TYCOD = Tycod;
                    tSent.SUB_TYCOD = Subcod;
                    tSent.SENT_DT = DateTime.Now.ToString("yyyyMMddHHmmss");
                    tSent.EMAIL_SENT = Comments;
                    tSent.NUM_1 = Num1;

                    TypeEntity.JC_HC_SENT.Add(tSent);
                    TypeEntity.SaveChanges();
                  }
                }
                else
                {
                  try
                  {
                    foreach (var tUser in typeUsers)
                    {
                      string tEmail = tUser.EMAIL;
                      string tLeo = tUser.LEO;

                      if (tLeo == "F")
                      {
                        CivEmails.Add(tEmail);
                      }
                      else
                      {
                        LeoEmails.Add(tEmail);
                      }

                      if (CivEmails.Count > 0)
                      {
                        CivEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv);
                      }

                      if (LeoEmails.Count > 0)
                      {
                        LeoEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv, callComms);
                      }
                    }
                  }
                  catch (OracleException ox)
                  {
                    MailException(ox);
                    LogException(ox);
                  }
                  catch (Exception ex)
                  {
                    MailException(ex);
                    LogException(ex);
                  }
                  finally
                  {
                    JC_HC_CURENT tUpdate = (from u in TypeEntity.JC_HC_CURENT
                                            where u.EID == Eid && u.NUM_1 == Num1 && u.AD_TS == Adts
                                            select u).FirstOrDefault();
                    if (tUpdate != null)
                    {
                      tUpdate.TYPE_EVAL = "T";
                      tUpdate.HC_SENT = "T";
                    }

                    JC_HC_SENT tSent = new JC_HC_SENT();
                    tSent.EID = Eid;
                    tSent.AG_ID = Agid;
                    tSent.TYCOD = Tycod;
                    tSent.SUB_TYCOD = Subcod;
                    tSent.SENT_DT = DateTime.Now.ToString("yyyyMMddHHmmss");
                    tSent.EMAIL_SENT = Comments;
                    tSent.NUM_1 = Num1;

                    TypeEntity.JC_HC_SENT.Add(tSent);
                    TypeEntity.SaveChanges();
                  }
                }
              }
            }
          }
          else
          {
            var tDefault = (from d in TypeEntity.JC_HC_CURENT
                            where d.TYPE_EVAL == "F"
                            select d).FirstOrDefault();
            tDefault.TYPE_EVAL = "T";
            TypeEntity.SaveChanges();
          }
        }
      }
      // Console.WriteLine("TypeEval exited at " + DateTime.Now.ToString("G"));
      return;
    }

    private static void CountEval()
    {
      // This function looks at the number of units arrived to the call within the first 25 minutes and if that threshhold has been met, the call is sent as a hot call.
      // Invoke a Console.WriteLine() statement for debugging purposes. It is commented out of production code.
      // Console.WriteLine("CountEval entered at " + DateTime.Now.ToString("G"));
      // DateTime cTime = DateTime.Now.AddMinutes(-60);
      hclogic CountEntity = new hclogic();
      var countCalls = from c in CountEntity.JC_HC_CURENT
                       where c.UNIT_COUNT > 0 && c.HC_SENT == "F"
                       select new
                       {
                         c.EID,
                         c.NUM_1,
                         c.TYCOD,
                         c.SUB_TYCOD,
                         c.ESTNUM,
                         c.EDIRPRE,
                         c.EFEANME,
                         c.EFEATYP,
                         c.AG_ID,
                         c.AD_TS,
                         c.XSTREET1,
                         c.XSTREET2,
                         c.ESZ,
                         c.COMMENTS,
                         c.UNIT_COUNT,
                         c.UNIT_EVAL,
                         c.HC_SENT
                       };

      foreach (var call in countCalls)
      {
        int Eid = call.EID;
        string Num1 = call.NUM_1;
        string Tycod = call.TYCOD;
        string Subcod = call.SUB_TYCOD;
        string Estnum = call.ESTNUM;
        string Edirpre = call.EDIRPRE;
        string Efeanme = call.EFEANME;
        string Efeatyp = call.EFEATYP;
        string Agid = call.AG_ID;
        string Adts = call.AD_TS;
        string Xstr1 = call.XSTREET1;
        string Xstr2 = call.XSTREET2;
        int? Esz = call.ESZ;
        string Comments = call.COMMENTS;
        int? UnitsArv = call.UNIT_COUNT;
        string Ueval = call.UNIT_EVAL;
        string Usent = call.HC_SENT;

        if (UnitsArv == null)
        {
          UnitsArv = 0;
        }

        string dispTime = Adts.Substring(8, 2) + ":" + Adts.Substring(10, 2);

        if (Ueval == "F")
        {
          DateTime cTime = DateTime.ParseExact(Adts.Substring(0, 14), formatString, null);
          int result = DateTime.Compare(cTime, compTime);
          if (result > 0)
          {
            var countLists = from c in CountEntity.JC_HC_AGENCY
                             select new
                             {
                               c.ID,
                               c.AG_ID,
                               c.UNITS
                             };
            foreach (var agency in countLists)
            {
              int cId = agency.ID;
              string cDept = agency.AG_ID;
              int cArv = agency.UNITS;

              var countUsers = from u in CountEntity.JC_HC_USERS
                               where (from g in CountEntity.JC_HC_USR_SND
                                      where g.AGY_ID == cId || g.AGY_ID == 17
                                      select new { g.USR_ID }).Equals(u.ID) && u.OOF == "F"
                               select new { u.EMAIL, u.LEO };

              if (Agid == cDept && UnitsArv >= cArv)
              {
                if (Efeanme != null)
                {
                  try
                  {
                    foreach (var user in countUsers)
                    {
                      string cEmail = user.EMAIL;
                      string cLeo = user.LEO;

                      if (cLeo == "F")
                      {
                        CivEmails.Add(cEmail);
                      }
                      else
                      {
                        LeoEmails.Add(cEmail);
                      }

                      if (CivEmails.Count > 0)
                      {
                        CivEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv);
                      }

                      if (LeoEmails.Count > 0)
                      {
                        LeoEmail(Agid, Tycod, Subcod, Estnum, Edirpre, Efeanme, Efeatyp, dispTime, Num1, UnitsArv, callComms);
                      }
                    }
                  }
                  catch (OracleException ox)
                  {
                    MailException(ox);
                    LogException(ox);
                  }
                  catch (Exception ex)
                  {
                    MailException(ex);
                    LogException(ex);
                  }
                  finally
                  {
                    var cUpdate = (from c in CountEntity.JC_HC_CURENT
                                   where c.EID == Eid && c.NUM_1 == Num1 && c.AD_TS == Adts
                                   select c).FirstOrDefault();

                    if (cUpdate != null)
                    {
                      cUpdate.UNIT_EVAL = "T";
                      cUpdate.HC_SENT = "T";
                    }

                    JC_HC_SENT cSent = new JC_HC_SENT();
                    cSent.EID = Eid;
                    cSent.AG_ID = Agid;
                    cSent.TYCOD = Tycod;
                    cSent.SUB_TYCOD = Subcod;
                    cSent.SENT_DT = DateTime.Now.ToString("yyyyMMddHHmmss");
                    cSent.EMAIL_SENT = Comments;
                    cSent.NUM_1 = Num1;

                    CountEntity.JC_HC_SENT.Add(cSent);
                    CountEntity.SaveChanges();
                  }
                }
                else
                {
                  try
                  {
                    foreach (var user in countUsers)
                    {
                      string cEmail = user.EMAIL;
                      string cLeo = user.LEO;

                      if (cLeo == "F")
                      {
                        CivEmails.Add(cEmail);
                      }
                      else
                      {
                        LeoEmails.Add(cEmail);
                      }

                      if (CivEmails.Count > 0)
                      {
                        CivEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv);
                      }

                      if (LeoEmails.Count > 0)
                      {
                        LeoEmail(Agid, Tycod, Subcod, Xstr1, Xstr2, dispTime, Num1, UnitsArv, callComms);
                      }
                    }
                  }
                  catch (OracleException ox)
                  {
                    MailException(ox);
                    LogException(ox);
                  }
                  catch (Exception ex)
                  {
                    MailException(ex);
                    LogException(ex);
                  }
                  finally
                  {
                    var cUpdate = (from c in CountEntity.JC_HC_CURENT
                                   where c.EID == Eid && c.NUM_1 == Num1 && c.AD_TS == Adts
                                   select c).FirstOrDefault();

                    if (cUpdate != null)
                    {
                      cUpdate.UNIT_EVAL = "T";
                      cUpdate.HC_SENT = "T";
                    }

                    JC_HC_SENT cSent = new JC_HC_SENT();
                    cSent.EID = Eid;
                    cSent.AG_ID = Agid;
                    cSent.TYCOD = Tycod;
                    cSent.SUB_TYCOD = Subcod;
                    cSent.SENT_DT = DateTime.Now.ToString("yyyyMMddHHmmss");
                    cSent.EMAIL_SENT = Comments;
                    cSent.NUM_1 = Num1;

                    CountEntity.JC_HC_SENT.Add(cSent);
                    CountEntity.SaveChanges();
                  }
                }
              }
            }
          }
          else
          {
            var cDefault = (from d in CountEntity.JC_HC_CURENT
                            where d.UNIT_EVAL == "F"
                            select d).FirstOrDefault();
            DateTime addDate = DateTime.ParseExact(cDefault.AD_TS.Substring(0, 14), formatString, null);
            DateTime coDate = DateTime.Now.AddMinutes(-25);
            int endResult = DateTime.Compare(addDate, coDate);
            if (endResult < 0)
            {
              cDefault.UNIT_EVAL = "T";
              CountEntity.SaveChanges();
            }
          }
        }
      }
      // Console.WriteLine("CountEval exited at " + DateTime.Now.ToString("G"));
      return;
    }

    private static void CivEmail(string Agid, string Tycod, string Subcod, string Xstr1, string Xstr2, string dispTime, string Num1, int? UnitsArv)
    {
      for (int i = 0; i < CivEmails.Count; i += batch)
      {
        using (MailMessage msg = new MailMessage())
        {
          msg.From = new MailAddress("jcso.hotcalls@jocogov.org", "HOT CALL");
          for (int j = i; (j < (i + batch)) && (j < CivEmails.Count); ++j)
          {
            msg.Bcc.Add(new MailAddress(CivEmails[j].Trim()));
          }
          msg.Subject = Agid.Trim() + " HOT CALL " + Tycod.Trim() + " " + Subcod.Trim();
          msg.Body = "Location: " + Xstr1.Trim() + " / " + Xstr2.Trim() + "\n";
          msg.Body += "Agency: " + Agid.Trim() + "\tTime Dispatched: " + dispTime.Trim() + "\tEvent No: " + Num1.Trim() + "\n";
          msg.Body += "Units Arrived: " + UnitsArv + "\tNature: " + Tycod.Trim() + " / " + Subcod.Trim();
          SmtpClient post = new SmtpClient();
          post.Send(msg);
        }
      }
    }

    private static void LeoEmail(string Agid, string Tycod, string Subcod, string Estnum, string Edirpre, string Efeanme, string Efeatyp, string dispTime, string Num1, int? UnitsArv, string[] callComms)
    {
      for (int i = 0; i < LeoEmails.Count; i += batch)
      {
        using (MailMessage msg = new MailMessage())
        {
          msg.From = new MailAddress("jcso.hotcalls@jocogov.org", "HOT CALL");
          for (int j = i; (j < (i + batch)) && (j < LeoEmails.Count); ++j)
          {
            msg.Bcc.Add(new MailAddress(CivEmails[j].Trim()));
          }
          msg.Subject = Agid.Trim() + " HOT CALL " + Tycod.Trim() + " " + Subcod.Trim();
          msg.Body = "Location: " + Estnum.Trim() + " " + Edirpre.Trim() + " " + Efeanme.Trim() + " " + Efeatyp.Trim() + "\n";
          msg.Body += "Agency: " + Agid.Trim() + "\tTime Dispatched: " + dispTime.Trim() + "\tEvent No.: " + Num1.Trim() + "\n";
          msg.Body += "Units Arrived: " + UnitsArv + "\tNature: " + Tycod.Trim() + " / " + Subcod.Trim() + "\n\n";
          foreach (string s in callComms)
          {
            msg.Body += s.Trim() + "\n";
          }
          SmtpClient post = new SmtpClient();
          post.Send(msg);
        }
      }
    }

    private static void LeoEmail(string Agid, string Tycod, string Subcod, string Xstr1, string Xstr2, string dispTime, string Num1, int? UnitsArv, string[] callComms)
    {
      for (int i = 0; i < LeoEmails.Count; i += batch)
      {
        using (MailMessage msg = new MailMessage())
        {
          msg.From = new MailAddress("jcso.hotcalls@jocogov.org", "HOT CALL");
          for (int j = i; (j < (i + batch)) && (j < LeoEmails.Count); ++j)
          {
            msg.Bcc.Add(new MailAddress(CivEmails[j].Trim()));
          }
          msg.Subject = Agid.Trim() + " HOT CALL " + Tycod.Trim() + " " + Subcod.Trim();
          msg.Body = "Location: " + Xstr1.Trim() + " / " + Xstr2.Trim() + "\n";
          msg.Body += "Agency: " + Agid.Trim() + "\tTime Dispatched: " + dispTime.Trim() + "\tEvent No.: " + Num1.Trim() + "\n";
          msg.Body += "Units Arrived: " + UnitsArv + "\tNature: " + Tycod.Trim() + " / " + Subcod.Trim() + "\n\n";
          foreach (string s in callComms)
          {
            msg.Body += s.Trim() + "\n";
          }
          SmtpClient post = new SmtpClient();
          post.Send(msg);
        }
      }
    }

    // The following sections are designed to move the Mailing and Logging features out of the individual methods and make them reusable.
    public static void MailException(OracleException ox)
    {
      MailMessage oMsg = new MailMessage();
      oMsg.From = new MailAddress("jcso.exception@jocogov.org", "Oracle Exception");
      oMsg.To.Add(new MailAddress("tony.dunsworht@jocogov.org", "Tony Dunsworth"));
      oMsg.To.Add(new MailAddress("carter.wetherington@jocogov.org", "Carter Wetherington"));
      oMsg.Subject = "Oracle Poller Exception";
      // Altered the original DateTime string to a standard .NET string for stability
      oMsg.Body = "Oracle has reported the following exception: " + ox.Number + " with the following explanation: " + ox.ToString() + " at " + DateTime.Now.ToString("G");
      SmtpClient oPost = new SmtpClient();
      oPost.Send(oMsg);
    }

    public static void MailException(Exception ex)
    {
      MailMessage eMsg = new MailMessage();
      eMsg.From = new MailAddress("jcso.exception@jocogov.org", "Oracle Exception");
      eMsg.To.Add(new MailAddress("tony.dunsworht@jocogov.org", "Tony Dunsworth"));
      eMsg.To.Add(new MailAddress("carter.wetherington@jocogov.org", "Carter Wetherington"));
      eMsg.Subject = "Hot Calls Poller Exception";
      // Altered the original DateTime string to a standard .NET string for stability
      eMsg.Body = "The Hot Calls Poller threw the following exception: " + ex.ToString() + " at " + DateTime.Now.ToString("G");
      SmtpClient ePost = new SmtpClient();
      ePost.Send(eMsg);
    }

    public static void LogException(OracleException ox)
    {
      FileStream pollErrorLog = null;
      pollErrorLog = File.Open(@"C:\Temp\pollErrorLog.txt", FileMode.Append, FileAccess.Write);
      StreamWriter pollErrorWrite = new StreamWriter(pollErrorLog);
      pollErrorWrite.WriteLine("Oracle has reported the following exception: " + ox.Number + " with the following explanation: " + ox.ToString() + " at " + DateTime.Now.ToString("G"));
      pollErrorWrite.Close();
      pollErrorLog.Close();
    }

    public static void LogException(Exception ex)
    {
      FileStream pollErrorLog = null;
      pollErrorLog = File.Open(@"C:\Temp\pollErrorLog.txt", FileMode.Append, FileAccess.Write);
      StreamWriter pollErrorWrite = new StreamWriter(pollErrorLog);
      pollErrorWrite.WriteLine("The Hot Calls Poller threw the following exception: " + ex.ToString() + " at " + DateTime.Now.ToString("G"));
      pollErrorWrite.Close();
      pollErrorLog.Close();
    }
  }
  // Change 20130208-1: Added additional columns to the database for up to 6 different agencies to be monitored. Putting in dev code to test for the weekend.
  // Change 20130304-1: Added the loi_grp_id field to the LOI selection information and changed the SQL query for email addresses to better isolate LOI CALLS going out to specific people.
  // Change 20130326-1: Added the Trim() method to the strings in the mailing methods to ensure that all unneeded white space is removed.
  // Change 20130327-1: Moved the SQL query to set the hc_sent flag to 'T' before sending the email.
  // Change 20130402-1: Moved the SQL query to write the sent mail to the jc_hc_sent_dev table before sending the email so OPD hot calls are not lost from the database.
  // Change 20130423-1: Added a if/else statement to the LOI email to account for LOI entries with no recipients.
  // Change 20130424-1: Modified the SQL query to adapt to a new field in the jc_hc_loi database to stop no recipient errors.
  // Change 20130628-1: Rewrote the email generation system to only generate 1 email for every 150 users. This should allow the program to run without additional errors for larger agencies as we will not breach the "too many recipients" threshold.
  // Change 20131029-1: Changed the table usage in the email address collection query to use one standard table jc_hc_usr_snd and ensure that works as designed. The query has been tested independently and brings back the expected numbers.
  // Change 20140226-1: Changed the DateTime formate in the Console.WriteLine and error reporting functions to "G" which is MS Long format. eg. 02/26/2014 4:02:03 PM
  // Change 20140226-2: Added the CivEmails List<T>() generic to aggregate non-LEO email addresses for separate treatment.
  // Change 20140226-3: Changed the email subroutines to enable Civilian and LEO emails to contain different information.
  // Change 20140311-1: Placed a try/catch block in the Main(string[] args) function to trap any errors and abort the process if there are problems.
  // Change 20140313-1: Removed legacy code for the original Main(string[] args) function as the current configuration has proven sufficiently reliable to not merit revisiting the old code.
  // Change 20140313-2: Moved the time comparison variable out of the individual methods and replaced it with a public DateTime variable.
  // Change 20140409-1: Switched code base to ODP.NET and Entity Framework v. 5.0.0
  // Change 20140409-2: Moved all queries to LINQ-To-Entities coding structure for simplicity
  // Change 20140409-3: Changed order of mailing structure to mail first and update tables second as originally designed using try/catch/finally block
  // Change 20140409-4: Added catch code to change evaluated calls from F to T if they do not rise to hot call status.
  // Change 20140409-5: Added where clauses to the LOI and UnitCount queries to use greater efficiency by not having to step through as many rows.
  // Change 20140409-6: First re-write with queries more optimised and removing additional non-used calls.
  // Change 20140411-1: Restructured the build for x64 architecture and x64 version of ODP.NET
  // Change 20140411-2: Reoptimised L2E queries and eliminated unnecessary object creation.
  // Change 20140411-3: Moved the batch size integer and the delimiter char[] out of the methods and made global variables.
  // Change 20140414-1: Re-wrote a TypeEval() LINQ query to bring it into one query not two.
  // Change 20140414-2: Changed the connection string in the app.config file to reflect the full TNSnames.ora entry to prevent ORA-12504 errors.
  // Change 20140418-1: Removed the last clause from the CountEval() method to ensure that calls aren't tossed until the 25 minute mark has passed and the call is closed out.
  // Change 20140418-2: Changed the SENT_DT string to DateTime.Now.ToString("yyyyMMddHHmmss") to better reflect when the call was actually sent out.
  // Change 20140825-1: Moved the Exception and Mailing functions out to separate overloaded methods for code efficiency. Removing over 160 lines.
  // Change 20140825-2: Updated LINQ queries to be more efficient.
  // Change 20140825-3: Updated variable names and conventions
  // Change 20140825-4: Moved several repeated pieces to become static variables at the beginning of the file, including both List<string>, the char[], and the mail from address. 
  // Change 20140825-5: Moved the address gathering queries out of the try blocks so it could be used in both halves of the if/else.
  // Change 20140825-6: Re-wrote the finally clause from the CountEval() method to ensure calls aren't tossed until 25 minute mark.
  // Change 20140825-7: Changed the CompareOrdinal to a DateTime.Compare after parsing for better accuracy.
}