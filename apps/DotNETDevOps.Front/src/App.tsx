
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';
import { VApp } from 'vuetify-tsx';


export interface AppOptions {

}

@Component
export default class App extends tsx.Component<AppOptions>{

    render() {
        return (
            <VApp>
                <router-view key={this.$route.fullPath}></router-view>
            </VApp>
        );
    }
}
