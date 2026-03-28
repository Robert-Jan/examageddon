using Examageddon.Data.Entities;
using Examageddon.Data.Enums;
using Examageddon.Services.Models;
using System.Text.Json;

namespace Examageddon.Services;

public static class QuestionImportService
{
    public const string TemplateJson = /*lang=json,strict*/ """
        [
          {
            "type": "MultipleChoice",
            "text": "What is the default retention period for Azure Monitor logs?",
            "allowMultipleAnswers": false,
            "scoringMode": "AllOrNothing",
            "options": [
              { "text": "30 days", "isCorrect": true },
              { "text": "7 days",  "isCorrect": false },
              { "text": "90 days", "isCorrect": false }
            ]
          },
          {
            "type": "MultipleChoice",
            "text": "Which services support availability zones?",
            "allowMultipleAnswers": true,
            "scoringMode": "PartialCredit",
            "options": [
              { "text": "Azure SQL Database", "isCorrect": true },
              { "text": "Azure VMs",          "isCorrect": true },
              { "text": "Azure DNS",          "isCorrect": false }
            ]
          },
          {
            "type": "Statement",
            "text": "Evaluate each statement about Azure Storage.",
            "options": [
              { "text": "Blob storage supports NFS 3.0",         "isCorrect": true },
              { "text": "Table storage is a relational database", "isCorrect": false }
            ]
          },
          {
            "type": "DragAndDrop",
            "text": "Match each Azure service to its primary purpose.",
            "pairs": [
              { "label": "Azure Blob Storage", "sentence": "Stores unstructured object data at scale" },
              { "label": "Azure Functions",    "sentence": "Runs event-driven serverless code" }
            ]
          }
        ]
        """;

    public static (List<StagedQuestion>? Questions, List<string> Errors) ParseAndValidate(string json)
    {
        var errors = new List<string>();
        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return (null, ["Invalid JSON file"]);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (null, ["File must contain a JSON array"]);
            }

            var questions = new List<StagedQuestion>();
            var items = doc.RootElement.EnumerateArray().ToList();

            for (var n = 0; n < items.Count; n++)
            {
                var q = items[n];
                var num = n + 1;

                var typeStr = q.TryGetProperty("type", out var tp) ? tp.GetString() : null;
                QuestionType? questionType = typeStr?.ToLowerInvariant() switch
                {
                    "multiplechoice" => QuestionType.MultipleChoice,
                    "statement" => QuestionType.Statement,
                    "draganddrop" => QuestionType.DragAndDrop,
                    _ => null,
                };

                if (questionType is null)
                {
                    errors.Add($"Question {num}: unknown type \"{typeStr ?? string.Empty}\"");
                    continue;
                }

                var text = q.TryGetProperty("text", out var txtProp) ? txtProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add($"Question {num}: text is required");
                }

                var allowMultiple = q.TryGetProperty("allowMultipleAnswers", out var amp) && amp.GetBoolean();

                var smStr = q.TryGetProperty("scoringMode", out var smp) ? smp.GetString() ?? string.Empty : string.Empty;
                var scoringMode = string.Equals(smStr, "partialcredit", StringComparison.OrdinalIgnoreCase)
                    ? MultiAnswerScoringMode.PartialCredit
                    : MultiAnswerScoringMode.AllOrNothing;

                var staged = new StagedQuestion
                {
                    QuestionType = questionType.Value,
                    Text = text ?? string.Empty,
                    AllowMultipleAnswers = allowMultiple,
                    ScoringMode = scoringMode,
                };

                if (questionType == QuestionType.DragAndDrop)
                {
                    ValidateDragAndDropPairs(q, num, errors, staged);
                }
                else
                {
                    ValidateOptions(q, num, questionType.Value, allowMultiple, errors, staged);
                }

                questions.Add(staged);
            }

            return errors.Count > 0 ? (null, errors) : (questions, []);
        }
    }

    public static Question ToQuestion(StagedQuestion staged, int examId, int orderIndex)
    {
        var forceMultiple = staged.QuestionType is QuestionType.DragAndDrop or QuestionType.Statement;

        var options = staged.Options.Select((opt, idx) => new AnswerOption
        {
            Text = opt.Text,
            MatchText = opt.MatchText,
            IsCorrect = staged.QuestionType == QuestionType.DragAndDrop
                ? !string.IsNullOrWhiteSpace(opt.Text) && !string.IsNullOrWhiteSpace(opt.MatchText)
                : opt.IsCorrect,
            OrderIndex = idx,
        }).ToList();

        return new Question
        {
            ExamId = examId,
            Text = staged.Text,
            QuestionType = staged.QuestionType,
            AllowMultipleAnswers = forceMultiple || staged.AllowMultipleAnswers,
            ScoringMode = staged.ScoringMode,
            OrderIndex = orderIndex,
            AnswerOptions = options,
        };
    }

    private static void ValidateDragAndDropPairs(JsonElement q, int num, List<string> errors, StagedQuestion staged)
    {
        if (!q.TryGetProperty("pairs", out var pairsProp)
            || pairsProp.ValueKind != JsonValueKind.Array
            || pairsProp.GetArrayLength() == 0)
        {
            errors.Add($"Question {num}: at least one pair is required");
            return;
        }

        var pairItems = pairsProp.EnumerateArray().ToList();
        for (var m = 0; m < pairItems.Count; m++)
        {
            var pair = pairItems[m];
            var pNum = m + 1;
            var label = pair.TryGetProperty("label", out var lp) ? lp.GetString() : null;
            var sentence = pair.TryGetProperty("sentence", out var sp) ? sp.GetString() : null;

            if (string.IsNullOrWhiteSpace(label))
            {
                errors.Add($"Question {num}, pair {pNum}: label is required");
            }

            if (string.IsNullOrWhiteSpace(sentence))
            {
                errors.Add($"Question {num}, pair {pNum}: sentence is required");
            }

            staged.Options.Add(new StagedOption
            {
                Text = sentence ?? string.Empty,
                MatchText = label ?? string.Empty,
                IsCorrect = true,
            });
        }
    }

    private static void ValidateOptions(JsonElement q, int num, QuestionType questionType, bool allowMultiple, List<string> errors, StagedQuestion staged)
    {
        if (!q.TryGetProperty("options", out var optsProp)
            || optsProp.ValueKind != JsonValueKind.Array
            || optsProp.GetArrayLength() == 0)
        {
            errors.Add($"Question {num}: at least one option is required");
            return;
        }

        var optItems = optsProp.EnumerateArray().ToList();
        var correctCount = 0;

        for (var m = 0; m < optItems.Count; m++)
        {
            var opt = optItems[m];
            var oNum = m + 1;
            var optText = opt.TryGetProperty("text", out var otp) ? otp.GetString() : null;
            var isCorrect = opt.TryGetProperty("isCorrect", out var icp) && icp.GetBoolean();

            if (questionType == QuestionType.Statement && string.IsNullOrWhiteSpace(optText))
            {
                errors.Add($"Question {num}, option {oNum}: text is required");
            }

            if (isCorrect)
            {
                correctCount++;
            }

            staged.Options.Add(new StagedOption { Text = optText ?? string.Empty, IsCorrect = isCorrect });
        }

        if (questionType == QuestionType.MultipleChoice)
        {
            if (correctCount == 0)
            {
                errors.Add($"Question {num}: at least one correct answer is required");
            }

            if (correctCount > 1 && !allowMultiple)
            {
                errors.Add($"Question {num}: multiple correct answers require allowMultipleAnswers: true");
            }
        }
    }
}
