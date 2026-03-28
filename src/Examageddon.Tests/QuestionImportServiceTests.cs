using Examageddon.Data.Enums;
using Examageddon.Services;

namespace Examageddon.Tests;

public class QuestionImportServiceTests
{
    [Fact]
    public void ParseAndValidate_ValidMultipleChoice_ReturnsStagedQuestion()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q1","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":false}]}]
            """;

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        Assert.Single(questions);
        Assert.Equal(QuestionType.MultipleChoice, questions[0].QuestionType);
        Assert.Equal("Q1", questions[0].Text);
        Assert.Equal(2, questions[0].Options.Count);
        Assert.True(questions[0].Options[0].IsCorrect);
        Assert.False(questions[0].Options[1].IsCorrect);
    }

    [Fact]
    public void ParseAndValidate_ValidDragAndDrop_NormalisesToStagedOptions()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"DragAndDrop","text":"Match","pairs":[
              {"label":"Blob","sentence":"Stores objects"}]}]
            """;

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        var q = questions[0];
        Assert.Equal(QuestionType.DragAndDrop, q.QuestionType);
        Assert.Single(q.Options);
        Assert.Equal("Blob", q.Options[0].MatchText);
        Assert.Equal("Stores objects", q.Options[0].Text);
    }

    [Fact]
    public void ParseAndValidate_ValidStatement_ReturnsStagedQuestion()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"Statement","text":"Evaluate","options":[
              {"text":"Blob supports NFS","isCorrect":true}]}]
            """;

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.NotNull(questions);
        Assert.Equal(QuestionType.Statement, questions[0].QuestionType);
    }

    [Fact]
    public void ParseAndValidate_TypeIsCaseInsensitive()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"multiplechoice","text":"Q","options":[{"text":"A","isCorrect":true}]}]
            """;

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Empty(errors);
        Assert.Equal(QuestionType.MultipleChoice, questions![0].QuestionType);
    }

    [Fact]
    public void ParseAndValidate_AllowMultipleAnswers_DefaultsFalse()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q","options":[{"text":"A","isCorrect":true}]}]
            """;

        var (questions, _) = QuestionImportService.ParseAndValidate(json);

        Assert.False(questions![0].AllowMultipleAnswers);
    }

    [Fact]
    public void ParseAndValidate_ScoringMode_DefaultsAllOrNothing()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q","allowMultipleAnswers":true,"options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (questions, _) = QuestionImportService.ParseAndValidate(json);

        Assert.Equal(MultiAnswerScoringMode.AllOrNothing, questions![0].ScoringMode);
    }

    [Fact]
    public void ParseAndValidate_ScoringModePartialCredit_Parsed()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q","allowMultipleAnswers":true,"scoringMode":"PartialCredit","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (questions, _) = QuestionImportService.ParseAndValidate(json);

        Assert.Equal(MultiAnswerScoringMode.PartialCredit, questions![0].ScoringMode);
    }

    [Fact]
    public void ParseAndValidate_InvalidJson_ReturnsError()
    {
        var (questions, errors) = QuestionImportService.ParseAndValidate("not json");

        Assert.Null(questions);
        Assert.Single(errors);
        Assert.Equal("Invalid JSON file", errors[0]);
    }

    [Fact]
    public void ParseAndValidate_RootNotArray_ReturnsError()
    {
        var (questions, errors) = QuestionImportService.ParseAndValidate(/*lang=json,strict*/ "{\"type\":\"MultipleChoice\"}");

        Assert.Null(questions);
        Assert.Single(errors);
        Assert.Equal("File must contain a JSON array", errors[0]);
    }

    [Fact]
    public void ParseAndValidate_UnknownType_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"Bogus","text":"Q"}]""";

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Null(questions);
        Assert.Contains(errors, e => e == "Question 1: unknown type \"Bogus\"");
    }

    [Fact]
    public void ParseAndValidate_MissingText_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"MultipleChoice","options":[{"text":"A","isCorrect":true}]}]""";

        var (questions, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Null(questions);
        Assert.Contains(errors, e => e == "Question 1: text is required");
    }

    [Fact]
    public void ParseAndValidate_MC_NoOptions_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"MultipleChoice","text":"Q"}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one option is required");
    }

    [Fact]
    public void ParseAndValidate_MC_NoCorrectAnswer_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"MultipleChoice","text":"Q","options":[{"text":"A","isCorrect":false}]}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one correct answer is required");
    }

    [Fact]
    public void ParseAndValidate_MC_MultipleCorrectWithoutFlag_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q","options":[
              {"text":"A","isCorrect":true},{"text":"B","isCorrect":true}]}]
            """;

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: multiple correct answers require allowMultipleAnswers: true");
    }

    [Fact]
    public void ParseAndValidate_Statement_NoOptions_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"Statement","text":"Q"}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one option is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_NoPairs_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"DragAndDrop","text":"Q"}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1: at least one pair is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_PairMissingLabel_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"DragAndDrop","text":"Q","pairs":[{"sentence":"S"}]}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1, pair 1: label is required");
    }

    [Fact]
    public void ParseAndValidate_DnD_PairMissingSentence_ReturnsError()
    {
        const string json = /*lang=json,strict*/ """[{"type":"DragAndDrop","text":"Q","pairs":[{"label":"L"}]}]""";

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e == "Question 1, pair 1: sentence is required");
    }

    [Fact]
    public void ParseAndValidate_ErrorsAreNumberedFrom1()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":"Q1","options":[{"text":"A","isCorrect":true}]},
             {"type":"MultipleChoice","text":"Q2"}]
            """;

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.Contains(errors, e => e.StartsWith("Question 2:"));
    }

    [Fact]
    public void ParseAndValidate_AllErrorsCollected_NotStoppedAtFirst()
    {
        const string json = /*lang=json,strict*/ """
            [{"type":"MultipleChoice","text":""},
             {"type":"DragAndDrop","text":""}]
            """;

        var (_, errors) = QuestionImportService.ParseAndValidate(json);

        Assert.True(errors.Count >= 2);
        Assert.Contains(errors, e => e.StartsWith("Question 1:"));
        Assert.Contains(errors, e => e.StartsWith("Question 2:"));
    }

    [Fact]
    public void ToQuestion_MC_MapsAllFields()
    {
        var staged = new Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.MultipleChoice,
            Text = "What is X?",
            AllowMultipleAnswers = false,
            ScoringMode = MultiAnswerScoringMode.AllOrNothing,
            Options =
            [
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = false },
            ],
        };

        var question = QuestionImportService.ToQuestion(staged, examId: 5, orderIndex: 2);

        Assert.Equal(5, question.ExamId);
        Assert.Equal("What is X?", question.Text);
        Assert.Equal(QuestionType.MultipleChoice, question.QuestionType);
        Assert.Equal(2, question.OrderIndex);
        Assert.Equal(2, question.AnswerOptions.Count);
        Assert.True(question.AnswerOptions.First(a => a.Text == "A").IsCorrect);
    }

    [Fact]
    public void ToQuestion_DnD_SetsAllowMultipleAnswersTrue()
    {
        var staged = new Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.DragAndDrop,
            Text = "Match",
            Options = [new() { Text = "S", MatchText = "L", IsCorrect = true }],
        };

        var question = QuestionImportService.ToQuestion(staged, examId: 1, orderIndex: 0);

        Assert.True(question.AllowMultipleAnswers);
        Assert.Equal("L", question.AnswerOptions.First().MatchText);
    }

    [Fact]
    public void ToQuestion_Statement_SetsAllowMultipleAnswersTrue()
    {
        var staged = new Services.Models.StagedQuestion
        {
            QuestionType = QuestionType.Statement,
            Text = "Evaluate",
            Options = [new() { Text = "Blob supports NFS", IsCorrect = true }],
        };

        var question = QuestionImportService.ToQuestion(staged, examId: 1, orderIndex: 0);

        Assert.True(question.AllowMultipleAnswers);
    }

    [Fact]
    public void GetTemplateJson_ReturnsValidJsonArray()
    {
        const string json = QuestionImportService.TemplateJson;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 3);
    }
}
