using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SFA.DAS.FundingRuleValidation.Jobs.Activities.Models;
using SFA.DAS.FundingRuleValidation.Jobs.Domain;

namespace SFA.DAS.FundingRuleValidation.Jobs.Activities;

public partial class CourseAgeCheckActivity(ILogger<CourseAgeCheckActivity> logger)
{
    [Function(nameof(CourseAgeCheckActivity))]
    public List<RuleCourseOutcome> Run([ActivityTrigger] RuleData ruleData, FunctionContext executionContext)
    {
        using var scope = logger.BeginScope(new List<KeyValuePair<string, object?>>
        {
            new ("CorrelationId", ruleData.Command.CorrelationId),
            new ("WaitingInstanceId", ruleData.Command.WaitingInstanceId),
        });
        
        var parameters = JsonSerializer.Deserialize<CourseAgeCheckParameters>(ruleData.Rule.Parameters)!;
        return ruleData.Command.Courses
            .Select(x =>
            {
                if (!CanApplyRule(x.TrainingType, x.StandardCode, ruleData.Rule.CourseIds))
                {
                    LogCourseDoesNotApplyToRule(x.Id, x.AimSequenceNumber);
                    return new RuleCourseOutcome(
                        ruleData.Rule.Id,
                        ruleData.Rule.IlrRuleName,
                        ruleData.Rule.IlrRuleDescription,
                        x.Id,
                        x.AimSequenceNumber,
                        RuleOutcome.Success,
                        []);
                }
                
                if (parameters.MinimumAge > x.AgeAtStartOfCourse || x.AgeAtStartOfCourse > parameters.MaximumAge)
                {
                    LogCourseCheckFailed(x.Id, x.AimSequenceNumber);
                    return new RuleCourseOutcome(
                        ruleData.Rule.Id,
                        ruleData.Rule.IlrRuleName,
                        ruleData.Rule.IlrRuleDescription,
                        x.Id,
                        x.AimSequenceNumber,
                        RuleOutcome.Error,
                        [new FundingRestriction(nameof(Course.AgeAtStartOfCourse), x.AgeAtStartOfCourse.ToString())]);
                }
            
                LogCourseCheckPassed(x.Id, x.AimSequenceNumber);
                return new RuleCourseOutcome(
                    ruleData.Rule.Id,
                    ruleData.Rule.IlrRuleName,
                    ruleData.Rule.IlrRuleDescription,
                    x.Id,
                    x.AimSequenceNumber,
                    RuleOutcome.Success,
                    []);
            })
            .ToList();
    }

    private static bool CanApplyRule(TrainingType trainingType, int? standardCode, HashSet<string> courseIds)
    {
        return trainingType == TrainingType.Standard && standardCode is not null && courseIds.Contains($"{standardCode}");
    }

    [LoggerMessage(LogLevel.Information, "CourseAgeCheckActivity failed for course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseCheckFailed(string courseId, int aimSequenceNumber);

    [LoggerMessage(LogLevel.Information, "CourseAgeCheckActivity passed for course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseCheckPassed(string courseId, int aimSequenceNumber);

    [LoggerMessage(LogLevel.Information, "CourseAgeCheckActivity does not apply to course {CourseId}-{AimSequenceNumber}")]
    partial void LogCourseDoesNotApplyToRule(string courseId, int aimSequenceNumber);
}