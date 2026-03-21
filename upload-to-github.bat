@echo off
chcp 65001 >nul
echo ========================================
echo 开始将文件上传到 GitHub...
echo ========================================

echo [1/3] 正在添加所有更改的文件...
git add .

echo [2/3] 正在生成提交...
:: 这里自动获取当前时间作为提交信息（Commit Message），方便你在 GitHub 上查看是什么时候提交的
git commit -m "Auto update: %date% %time%"

echo [3/3] 正在推送到 GitHub...
:: 将代码推送到远程仓库
git push

echo ========================================
echo 上传已完成！
echo ========================================
pause