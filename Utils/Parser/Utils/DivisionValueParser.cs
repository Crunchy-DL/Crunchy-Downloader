namespace CRD.Utils.Parser.Utils;

public class DivisionValueParser{
    public static double ParseDivisionValue(string value){
        string[] parts = value.Split('/');
        double result = double.Parse(parts[0]);
        for (int i = 1; i < parts.Length; i++){
            result /= double.Parse(parts[i]);
        }

        return result;
    }
}