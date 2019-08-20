﻿/********************************************************************
 License: https://github.com/chengderen/Smartflow/blob/master/LICENSE 
 Home page: https://www.smartflow-sharp.com
 Github : https://github.com/chengderen/Smartflow-Sharp
 ********************************************************************
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Smartflow.Elements;


namespace Smartflow
{
    public  class WorkflowEngine
    {
        private readonly static WorkflowEngine singleton = new WorkflowEngine();
        private IWorkflow workflowService = WorkflowGlobalServiceProvider.OfType<IWorkflow>();

        protected WorkflowEngine()
        {

        }

        /// <summary>
        /// 全局的插件
        /// </summary>
        protected IList<IPlugin> Plugins
        {
            get{ return WorkflowGlobalServiceProvider.Query<IPlugin>();}
        }

        /// <summary>
        /// 默认的插件解析服务
        /// </summary>
        protected IResolve Resolve
        {
            get { return WorkflowGlobalServiceProvider.OfType<IResolve>(); }
        }

        public static WorkflowEngine Instance
        {
            get { return singleton; }
        }

        /// <summary>
        /// 根据传递的流程XML字符串,启动工作流
        /// </summary>
        /// <param name="resourceXml"></param>
        /// <returns></returns>
        public string Start(string resourceXml)
        {
            return workflowService.Start(resourceXml);
        }

        /// <summary>
        /// 进行流程跳转
        /// </summary>
        /// <param name="context"></param>
        public void Jump(WorkflowContext context)
        {
            WorkflowInstance instance = context.Instance;
            if (instance.State == WorkflowInstanceState.Running)
            {
                WorkflowNode current = instance.Current;

                string transitionTo = current.Transitions
                                  .FirstOrDefault(e => e.NID == context.TransitionID).Destination;

                ASTNode to = current.GetNode(transitionTo);

                Process(context,to);

                instance.Jump(transitionTo);

                Processing(new ExecutingContext()
                {
                    From = current,
                    To = to,
                    TransitionID = context.TransitionID,
                    Instance = instance,
                    Data = context.Data,
                    ActorID = context.ActorID,
                    ActorName = context.ActorName
                });

                if (to.NodeType == WorkflowNodeCategory.End)
                {
                    instance.Transfer(WorkflowInstanceState.End);
                }
                else if (to.NodeType == WorkflowNodeCategory.Decision)
                {
                    WorkflowDecision wfDecision = WorkflowDecision.ConvertToReallyType(to);
                    Transition transition = wfDecision.GetTransition();
                    if (transition == null) return;
                    Jump(new WorkflowContext()
                    {
                        Instance = WorkflowInstance.GetInstance(instance.InstanceID),
                        TransitionID = transition.NID,
                        ActorID = context.ActorID,
                        Data = context.Data
                    });
                }
            }
        }

        /// <summary>
        /// 跳转过程处理入库
        /// </summary>
        /// <param name="executeContext">执行上下文</param>
        protected void Processing(ExecutingContext executeContext)
        {
            List<IPlugin> allPlugin = new List<IPlugin>();
            allPlugin.AddRange(this.Plugins);
            //allPlugin.Add(Resolve.Scan());
            workflowService.Processing(new WorkflowProcess()
            {
                RelationshipID = executeContext.To.NID,
                Origin = executeContext.From.ID,
                Destination = executeContext.To.ID,
                TransitionID = executeContext.TransitionID,
                InstanceID = executeContext.Instance.InstanceID,
                NodeType = executeContext.From.NodeType
            });

            allPlugin.ForEach(pluin => pluin.ActionExecuted(executeContext));
        }

        /// <summary>
        ///  跳转前
        /// </summary>
        /// <param name="context"></param>
        /// <param name="to"></param>
        protected void Process(WorkflowContext context, ASTNode to)
        {
            List<IPlugin> allPlugin = new List<IPlugin>();
            allPlugin.AddRange(this.Plugins);
            //allPlugin.Add(Resolve.Scan());
            allPlugin.ForEach(pluin => pluin.ActionExecute(context));
        }
    }
}
