
import * as tsx from "vue-tsx-support";
import { Component, Prop, Watch } from 'vue-property-decorator';
 

export interface FrontPageOptions {

}

@Component(
    {
        layout: "DefaultLayout"
    }
)
export default class FrontPage extends tsx.Component<FrontPageOptions>{




    mounted() {
        console.log("hello wolrd");
    }

    render() {

        return (
            <div class="section main">
                <div>Hello World</div>
                </div>
        );
    }
}
