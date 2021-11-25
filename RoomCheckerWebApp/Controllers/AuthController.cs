using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace RoomChecker.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SilentStart()
        {
            return View();
        }

        public IActionResult SilentEnd()
        {
            return View();
        }
    }
}