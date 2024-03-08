# BikaDownloader

用于下载用户收藏夹中的本子，支持增量下载功能。

## 用法

1. 创建一个工作区文件夹(建议创建，因为程序会在当前所在目录下载输出文件)
2. 打开命令行，进入该文件夹
3. [下载程序文件](./releases)到该文件夹
4. 在User目录下创建bika-downloader.config.json，写明账户名和密码
    ```json
    {
      "username": "xxxxx",
      "password": "xxxxx"
    }
    ```
    User目录windows下就在"C/Users/[你自己的用户名]"，MacOS在"/Macintosh HD/用户/[你自己的用户名]"，linux在"/home/[你自己的用户名]"
    通常在命令行内使用`cd ~`就可以进入User目录
5. 在命令行内运行该程序 
    ```bash
      # 尾缀根据目标系统不同而不同这里不赘述
      ./Bika.Downloader-windows-x86 
    ```

## 退出下载程序

使用`ctrl+c`即可退出下载程序

## 常见错误
- ```
    fail: Microsoft.Extensions.Hosting.Internal.Host[9]
          BackgroundService failed
          System.Net.Http.HttpRequestException: The SSL connection could not be established, see inner exception.
    ```
    梯子连不上，解决办法换节点
