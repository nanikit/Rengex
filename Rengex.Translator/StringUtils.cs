namespace Rengex.Translator {

  public class StringUtils {

    public static int CountLines(string str) {
      char[] ar = str.ToCharArray();
      int len = ar.Length;
      int cnt = 0;
      for (int i = 0; i < len; i++) {
        char c = ar[i];
        if (c == '\n') {
          cnt++;
        }
        else if (c == '\r') {
          cnt++;
          int ni = i + 1;
          if (ni < len && ar[ni] == '\n') {
            i++;
          }
        }
      }
      return cnt;
    }
  }
}
