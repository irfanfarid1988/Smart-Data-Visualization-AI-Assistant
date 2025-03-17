using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SDVA.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(Models.LoginModel model)
        {
            if (ModelState.IsValid)
            {
                List<SqlParameter> Params = new List<SqlParameter>();

                SqlDatabase objDb = new SqlDatabase();
                string sqlLogin = "SELECT USERID,NAME,EMAIL FROM USERS WHERE EMAIL=@parEmail and PASSWORD=@parPassword COLLATE SQL_Latin1_General_CP1_CS_AS ";
                try
                {
                    objDb.AddParameter(ref Params, "parEmail", model.UserEmail, SqlDbType.NVarChar, 100);
                    objDb.AddParameter(ref Params, "parPassword", model.Password, SqlDbType.NVarChar, 100);
                    
                    DataTable dtUser =objDb.getDataTable(sqlLogin, Params);

                    if (dtUser != null & dtUser.Rows.Count>0)
                    {
                        Session["USERID"] = dtUser.Rows[0]["USERID"].ToString();
                        Session["NAME"] = dtUser.Rows[0]["NAME"].ToString();
                        Session["EMAIL"] = dtUser.Rows[0]["EMAIL"].ToString();

                        return RedirectToAction("Chat", "Chat");
                    }
                    else
                    {
                        //ModelState.AddModelError("", "Invalid username or password.");
                        // Display a custom error message to the user
                        ViewBag.ErrorMessage = "Invalid username or password.";
                        return View(model);
                    }
                }
                
                catch (Exception ex)
                {
                    // Display the exception message to the user
                    ViewBag.ErrorMessage = ex.Message;
                    return View(model);
                }
                finally
                {
                    Params.Clear();
                    Params = null;
                    objDb=null;
                    GC.Collect();
                }
            }
            else
                {
                    // Display the validation errors to the user
                    ViewBag.ErrorMessage = string.Join(";", ModelState.Values
                                            .SelectMany(x => x.Errors)
                                            .Select(x => x.ErrorMessage.Trim()));
                    return View(model);
                }

            return View(model);
        }
        public ActionResult Logout()
        {
            Session["Username"] = null;
            Session.Clear();
            Session.Abandon();
            Session.RemoveAll();
            return RedirectToAction("Login", "Account");
        }
    }

}