using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MoneyCalculatorFunctions.Entities;
using MoneyCalculatorFunctions.Services;
using Newtonsoft.Json;

namespace MoneyCalculatorFunctions
{
    public class MortgageFunctions
    {
        internal const string LoanQueryKey = "loan";
        internal const string InterestQueryKey = "interest";
        internal const string NumberOfPaymentsQueryKey = "nPayments";
        
        private readonly IMortgageCalculator mortgageCalculator;

        public MortgageFunctions(IMortgageCalculator mortgageCalculator)
        {
            if (mortgageCalculator == null)
                throw new ArgumentNullException(nameof(mortgageCalculator));

            this.mortgageCalculator = mortgageCalculator;
        }

        [FunctionName(FunctionNames.MortgageCalculatorFunction)]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            [Table("executionsTable", Connection = "StorageAccount")] ICollector<ExecutionRow> outputTable,
            ILogger log)
        {
            log.LogInformation($"{FunctionNames.MortgageCalculatorFunction} start");

            // Retrieve loan, interest and numberOfPayments from HTTP Request
            #region [ Retrieve request parameters ]
            decimal loan;
            if (!GetLoanFromQueryString(req, out loan))
            {
                log.LogError($"Loan not valid");
                return new BadRequestObjectResult("Loan not valid");
            }

            double interest;
            if (!GetInterestFromQueryString(req, out interest))
            {
                log.LogError($"Annual Interest not valid");
                return new BadRequestObjectResult("Annual Interest not valid");
            }

            uint nPayments;
            if (!GetNumberOfPaymentsFromQueryString(req, out nPayments))
            {
                log.LogError($"Number of payments not valid");
                return new BadRequestObjectResult("Number of payments not valid");
            }
            #endregion [ Retrieve request parameters ]

            var calculatorResult =
                await this.mortgageCalculator.CalculateMontlyRateAsync(loan, interest, nPayments);

            #region [ Create the response ]
            var executionRow = new ExecutionRow(DateTime.Now)
            {
                AnnualInterest = interest,
                ErrorCode = calculatorResult.Error?.Code,
                MonthlyRate = calculatorResult.Result,
                MortgageLoan = loan,
                NumberOfPayments = nPayments,
                Result = calculatorResult.Succeed
            };

            outputTable.Add(executionRow);
            #endregion [ Create the response ]

            if (calculatorResult.Succeed)
            {
                return new OkObjectResult(calculatorResult.Result);
            }

            return new BadRequestObjectResult(calculatorResult.Error.Message);
        }

        #region [ Private Methods ]
        private bool GetLoanFromQueryString(HttpRequest req, out decimal loan)
        {
            loan = 0;
            string queryLoan = req.Query[LoanQueryKey];
            if (queryLoan == null || !decimal.TryParse(queryLoan, out loan))
                return false;
            return true;
        }

        private bool GetInterestFromQueryString(HttpRequest req, out double interest)
        {
            interest = 0;
            string queryInterest = req.Query[InterestQueryKey];
            if (queryInterest == null || !double.TryParse(queryInterest, out interest))
                return false;
            return true;
        }

        private bool GetNumberOfPaymentsFromQueryString(HttpRequest req, out uint nPayments)
        {
            nPayments = 0;
            string querynPayments = req.Query[NumberOfPaymentsQueryKey];
            if (querynPayments == null || !uint.TryParse(querynPayments, out nPayments))
                return false;
            return true;
        }
        #endregion [ Private Methods ]
    }
}
