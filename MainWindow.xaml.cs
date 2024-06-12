using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using MathNet.Symbolics;
using MathNetSymbolics = MathNet.Symbolics;

namespace PEMDAS
{
    public partial class MainWindow : Window
    {
        private readonly CalculationFacade _calculationFacade;
        private readonly Originator _originator = new Originator();
        private readonly Stack<Memento> _history = new Stack<Memento>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadHistory();
            var factory = new DecoratedCalculationFactory(SetResultTextBox);
            _calculationFacade = new CalculationFacade(factory);
        }

        private void InitializeDatabase()
        {
            using (var context = new ApplicationDbContext())
            {
                context.Database.EnsureCreated();
            }
        }

        private void SaveState()
        {
            _originator.Expression = FormulaTextBox.Text;
            _originator.Result = ResultTextBox.Text;
            _history.Push(_originator.SaveToMemento());
        }

        private void RestoreState()
        {
            if (_history.Count > 0)
            {
                var memento = _history.Pop();
                _originator.RestoreFromMemento(memento);
                FormulaTextBox.Text = _originator.Expression;
                ResultTextBox.Text = _originator.Result;
            }
        }

        private void ExecuteCalculation(ICalculationStrategy strategy)
        {
            try
            {
                string input = FormulaTextBox.Text;
                var command = new StrategyCommand(input, strategy, result => ResultTextBox.Text = result);
                command.Execute(null);
                SaveCalculation(input, ResultTextBox.Text);
                SaveState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Check your entering");
            }
        }

        private void OnSimplifyClick(object sender, RoutedEventArgs e)
        {
            var strategy = new SimplifyStrategy();
            ExecuteCalculation(strategy);
        }

        private void OnDifferentiateClick(object sender, RoutedEventArgs e)
        {
            var strategy = new DifferentiateStrategy();
            ExecuteCalculation(strategy);
        }

        private void OnHistorySelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selectedHistory = (CalculationHistory)HistoryListBox.SelectedItem;
            if (selectedHistory != null)
            {
                var formula = new Formula(selectedHistory.Expression).Clone();
                FormulaTextBox.Text = formula.Expression;
                ResultTextBox.Text = selectedHistory.Result;
            }
        }

        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            RestoreState();
        }

        private void SaveCalculation(string expression, string result)
        {
            using (var context = new ApplicationDbContext())
            {
                var history = new CalculationHistory
                {
                    Expression = expression,
                    Result = result,
                    Timestamp = DateTime.Now
                };
                context.CalculationHistories.Add(history);
                context.SaveChanges();
                LoadHistory();
            }
        }

        private void LoadHistory()
        {
            using (var context = new ApplicationDbContext())
            {
                HistoryListBox.ItemsSource = context.CalculationHistories.OrderByDescending(h => h.Timestamp).ToList();
            }
        }

        private void SetResultTextBox(string result)
        {
            Dispatcher.Invoke(() => ResultTextBox.Text = result);
        }
    }


    public interface ICalculation
    {
        string Compute(string expression);
    }

    public class SimplifyCalculation : ICalculation
    {
        public string Compute(string expression)
        {
            var simplifiedExpression = Infix.Format(Algebraic.Expand(Infix.ParseOrThrow(expression)));
            return simplifiedExpression;
        }
    }

    public class DifferentiateCalculation : ICalculation
    {
        public string Compute(string expression)
        {
            var expr = MathNetSymbolics.Infix.ParseOrThrow(expression);
            var symbol = MathNetSymbolics.Expression.Symbol("x");
            var differentiatedExpression = MathNetSymbolics.Calculus.Differentiate(symbol, expr);
            return MathNetSymbolics.Infix.Format(differentiatedExpression);
        }
    }
    // abstract factory
    public interface ICalculationFactory
    {
        ICalculation CreateSimplifyCalculation();
        ICalculation CreateDifferentiateCalculation();
    }

    public class CalculationFactory : ICalculationFactory
    {
        public ICalculation CreateSimplifyCalculation()
        {
            return new SimplifyCalculation();
        }

        public ICalculation CreateDifferentiateCalculation()
        {
            return new DifferentiateCalculation();
        }
    }
    //

    // DECORATOR PATTERN
    public interface ICalculationDecorator : ICalculation
    {
    }

    public class CalculationDecorator : ICalculationDecorator
    {
        protected readonly ICalculation _calculation;

        public CalculationDecorator(ICalculation calculation)
        {
            _calculation = calculation;
        }

        public virtual string Compute(string expression)
        {
            return _calculation.Compute(expression);
        }
    }

    public class LoggingDecorator : CalculationDecorator
    {
        private readonly Action<string> _setResultAction;

        public LoggingDecorator(ICalculation calculation, Action<string> setResultAction) : base(calculation)
        {
            _setResultAction = setResultAction;
        }

        public override string Compute(string expression)
        {
            var result = base.Compute(expression);
            _setResultAction(result);
            return result;
        }
    }

    //abstract factory + decorator 
    public class DecoratedCalculationFactory : ICalculationFactory
    {
        private readonly Action<string> _setResultAction;

        public DecoratedCalculationFactory(Action<string> setResultAction)
        {
            _setResultAction = setResultAction;
        }

        public ICalculation CreateSimplifyCalculation()
        {
            return new LoggingDecorator(new SimplifyCalculation(), _setResultAction);
        }

        public ICalculation CreateDifferentiateCalculation()
        {
            return new LoggingDecorator(new DifferentiateCalculation(), _setResultAction);
        }
    }

    // FACADE PATTERN
    public class CalculationFacade
    {
        private readonly ICalculationFactory _calculationFactory;

        public CalculationFacade(ICalculationFactory calculationFactory)
        {
            _calculationFactory = calculationFactory;
        }

        public string Simplify(string expression)
        {
            var calculation = _calculationFactory.CreateSimplifyCalculation();
            return calculation.Compute(expression);
        }

        public string Differentiate(string expression)
        {
            var calculation = _calculationFactory.CreateDifferentiateCalculation();
            return calculation.Compute(expression);
        }
    }
    //

    // MEMENTO PATTERN
    public class Memento
    {
        public string Expression { get; }
        public string Result { get; }

        public Memento(string expression, string result)
        {
            Expression = expression;
            Result = result;
        }
    }

    public class Originator
    {
        public string Expression { get; set; }
        public string Result { get; set; }

        public Memento SaveToMemento()
        {
            return new Memento(Expression, Result);
        }

        public void RestoreFromMemento(Memento memento)
        {
            Expression = memento.Expression;
            Result = memento.Result;
        }
    }
    //

    // STRATEGY PATTERN
    public interface ICalculationStrategy
    {
        string Execute(string expression);
    }

    public class SimplifyStrategy : ICalculationStrategy
    {
        public string Execute(string expression)
        {
            var simplifiedExpression = Infix.Format(Algebraic.Expand(Infix.ParseOrThrow(expression)));
            return simplifiedExpression;
        }
    }

    public class DifferentiateStrategy : ICalculationStrategy
    {
        public string Execute(string expression)
        {
            var expr = MathNetSymbolics.Infix.ParseOrThrow(expression);
            var symbol = MathNetSymbolics.Expression.Symbol("x");
            var differentiatedExpression = MathNetSymbolics.Calculus.Differentiate(symbol, expr);
            return MathNetSymbolics.Infix.Format(differentiatedExpression);
        }
    }
    //


    // command PATTERN
    public class StrategyCommand : ICommand
    {
        private readonly string _expression;
        private readonly ICalculationStrategy _strategy;
        private readonly Action<string> _callback;

        public StrategyCommand(string expression, ICalculationStrategy strategy, Action<string> callback)
        {
            _expression = expression;
            _strategy = strategy;
            _callback = callback;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            string result = _strategy.Execute(_expression);
            _callback(result);
        }
    }
    //

    // prototype pattern
    public class Formula : IPrototype<Formula>
    {
        public string Expression { get; set; }

        public Formula(string expression)
        {
            Expression = expression;
        }

        public Formula Clone()
        {
            return new Formula(this.Expression);
        }

        public Formula DeepClone()
        {
            return new Formula(new string(this.Expression.ToCharArray()));
        }
    }

    public interface IPrototype<T>
    {
        T Clone();
    }
}
