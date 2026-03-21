# 网页工程维护说明

## 一键整理
在项目根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\organize-project.ps1
```

说明：
- 该脚本可重复执行（幂等），会自动补齐目录、迁移文件、修正路径并做基本校验。
- 如果只想预览动作，不实际修改，可加 `-DryRun`：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\organize-project.ps1 -DryRun
```

## 一键内容管理（推荐日常使用）
- 双击 `manage-site.bat`：进入中文菜单，支持新增/删除/编辑/排版/发布/导入本地HTML/添加外部链接。
- 双击 `publish-site.bat`：不进入菜单，直接一键同步网页。

对应脚本：
- `scripts/site-manager.ps1`
- 内容数据库：`content/site-content.json`

## 当前目录约定
- `index.html`：主页入口
- `config.js`：轮播卡片数据（标题、图片、跳转链接）
- `style.css` / `style.scss`：主页样式
- `pages/core`：正式子页面（author/link1/link2/link3）
- `pages/experiments`：实验或草稿页面（t1/t2/t3/task01/task02）
- `asset/image/cards`：主页轮播图
- `asset/image/backgrounds`：背景素材
- `asset/image/gallery`：通用展示图
- `asset/image/archive`：历史/学习素材

## 日常更新流程
1. 把新图片放到对应目录（轮播图放 `asset/image/cards`）。
2. 在 `config.js` 更新 `title/image/link/alt`。
3. 本地预览 `index.html`，确认图片和跳转正常。
4. 执行一遍整理脚本，避免路径和结构漂移。

## 注意
- `pages/experiments/task02.html` 目前是“说明文本 + 代码块”格式，不是可直接运行页面。如需上线请先清理成纯 HTML。

