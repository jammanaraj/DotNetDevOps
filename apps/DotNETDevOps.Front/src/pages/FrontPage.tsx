
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';

import { VBtn } from "vuetify-tsx"

import "@/assets/less/components/sections.less";
//import "@/assets/less/components/btn.less";


export interface FrontPageOptions {

}


export interface WDynItemOptions {
    number: string;
    title: string;
}
@Component
export class WDynItem extends tsx.Component<WDynItemOptions> {

    resourceApi = "https://management.dotnetdevops.org";

    @Prop({default:"01"})
    number!: string;

    @Prop()
    title!: string;

    render() {
        return (
        <div class="wrapper w-dyn-item">
            <div class="column">
                <div class="column _100vh">
                    <div class="project-info">
                        <div class="number">
                            <h2 class="number zero">{this.number}</h2>
                        </div>
                        <h2 class="project-title">{this.title}</h2>

                        <VBtn target="_blank" href={`https://portal.azure.com/#create/Microsoft.Template/uri/${encodeURI(this.resourceApi + '/providers/DotNetDevOps.AzureTemplates/templates/azure-function')}`}>Deploy to Azure</VBtn>


                    </div>
                </div>
                <div class="column _100vh bg-white">
                    <div class="project-description">
                        <p class="reader">
                            With the following Azure Function, using lets encrypt is easy.
                                        </p>
                        <p class="reader">

                        </p>
                    </div>
                </div>
            </div>
            </div>
        )
    }
}

@Component
export default class FrontPage extends tsx.Component<FrontPageOptions>{

   

    


    render() {
        console.log(this.$slots);
        return (
            <div class="section main">
                <div class="w-dyn-list">
                    <div class="w-dyn-items">
                        {
                            this.$slots.default
                        }
                    </div>
                </div>

            </div>
        );
    }
}
