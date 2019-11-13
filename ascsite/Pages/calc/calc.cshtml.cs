using ascsite.Core;

using ascsite.Core.AscSci.Calculator;
using ascsite.Core.PyInterface;
using ascsite.Core.PyInterface.PyMath;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using ascsite;
using System.IO;
using System.Threading;
using processor;

namespace ascsite
{
    public class CalcModel : PageModel
    {
        public string CalcResponse { get; set; }
        public string PlainCalcResponse { get; set; }
        public string InterpretedAs { get; set; }
        public string Error { get; set; }
        public string Visib1 { get; set; }
        public string Visib2 { get; set; }
        public string Visib3 { get; set; }
        public string Visib4 { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Expression { get; set; }

        public void Process(string expr)
        {
            Exception exc = null;
            var calcTask = new Thread(() =>
            {
                try
                {
                    if (!Security.ExprSecure(Expression))
                        throw new SecurityException();
                    AscCalc calc = new AscCalc(Expression, latex: true);
                    CalcResult cr;
                    cr = calc.Compute();
                    CalcResponse = cr.Result;
                    InterpretedAs = string.Join("<br>", cr.InterpretedAs);

                    AscCalc calc_pl = new AscCalc(Expression, latex: false);
                    cr = calc_pl.Compute();
                    PlainCalcResponse = cr.Result;
                }
                catch(Exception e)
                {
                    exc = e;
                }
            });
            calcTask.Start();

            if (!calcTask.Join(millisecondsTimeout: Const.LIMIT_CALC_EXECUTE_MS))
                throw new TimeOutException(Const.LIMIT_MSL_EXECUTE_MS + "ms passed");
            if (exc != null)
                throw exc;
            // TO DO
            // @"\[x = {-b \pm \sqrt{b^2-4ac} \over 2a}.\] change this in Procees(Expression) method for latex expression";
        }

        public void ShowIntepretedAs() { Visib1 = "block";  }
        public void ShowResult() { Visib2 = "block"; }
        public void ShowError() { Visib3 = "block"; }
        public void ShowExamples() { Visib4 = "block"; }

        public void OnGet()
        {
            Visib1 = "none";
            Visib2 = "none";
            Visib3 = "none";
            Visib4 = "none";
            if (!string.IsNullOrEmpty(Expression))
            {

                try
                {
                    Process(Expression);
                    ShowIntepretedAs();
                    ShowResult();
                }
                catch (Exception e)
                {
                    Error = Const.ERMSG_UNABLE_TO_SOLVE;
                    Console.WriteLine("Error: " + e.Message);
                    ShowError();
                    ShowExamples();
                }
            }
            else
            {
                if(Expression != null)
                {
                    ShowError();
                    ShowExamples();
                    Error = Const.ERMSG_EMPTYREQ;
                }
                else
                {
                    ShowExamples();
                }
            }
        }

        public void OnPost()
        {
            // to do
        }
    }
}