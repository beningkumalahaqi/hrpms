using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EnterpriseHR.WebApp.Models;

namespace EnterpriseHR.WebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Reached two ways: UseExceptionHandler("/Home/Error") without a code in non-Development,
    // and UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}") for body-less MVC
    // statuses. The original status is put back on the response so a 404 page answers 404.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        if (code is int statusCode)
        {
            Response.StatusCode = statusCode;
        }

        ViewData["StatusCode"] = code;
        ViewData["Title"] = code switch
        {
            StatusCodes.Status404NotFound => "Page not found",
            >= StatusCodes.Status500InternalServerError => "Something went wrong",
            _ => "Error",
        };

        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
